using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

// FROM:
// AlertResources.AlertDescriptionTimeAheadFormat(machine, delta.Absolute().ToDisplayString())
// TO:
// string.Format(AlertResources.Culture, AlertResources.AlertDescriptionTimeAhead, machine, delta.Absolute().ToDisplayString());

internal class ResourcesExSourceVisitor : ISourceVisitor
{
    public async Task<Document> VisitAsync(Document document)
    {
        bool hasUpdates;
        do
        {
            hasUpdates = false;

            var root = (CompilationUnitSyntax?)await document.GetSyntaxRootAsync();
            if (root is null)
                return document;

            var allInvocationExprs = root.DescendantNodes().OfType<InvocationExpressionSyntax>().ToList();

            foreach (var invocationExpr in allInvocationExprs)
            {
                var context = await ValidateInvocationExpressionASync(document, invocationExpr);
                if (context is not null)
                {
                    var newSyntax = CreateUpdatedExpression(context);
                    if (invocationExpr.HasLeadingTrivia)
                    {
                        newSyntax = newSyntax.WithLeadingTrivia(invocationExpr.GetLeadingTrivia());
                    }
                    var updatedRoot = root.ReplaceNode(invocationExpr, newSyntax);
                    document = document.WithSyntaxRoot(updatedRoot);
                    hasUpdates = true;
                    break;
                }
            }
        }
        while (hasUpdates);

        return document;
    }

    private static async Task<ResourcesExContext?> ValidateInvocationExpressionASync(Document document, InvocationExpressionSyntax invocationExpr)
    {
        var memberAccessExpr = invocationExpr.Expression as MemberAccessExpressionSyntax;
        if (memberAccessExpr is null)
            return null;
        var resourcesClassExpr = memberAccessExpr.Expression as IdentifierNameSyntax;
        if (resourcesClassExpr is null)
            return null;

        var methodName = memberAccessExpr.Name.ToString();
        if (!methodName.EndsWith("Format"))
            return null;

        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel is null)
            return null;

        if (!ReferenceEquals(semanticModel.SyntaxTree, invocationExpr.SyntaxTree))
        {
            semanticModel = semanticModel.Compilation.GetSemanticModel(invocationExpr.SyntaxTree);
        }

        var isLegacyResourcesClass = semanticModel.GetTypeInfo(resourcesClassExpr).Type?.GetAttributes()
            .Any(x => x.ConstructorArguments.Length > 0
                && "DMKSoftware.CodeGenerators.Tools.StronglyTypedResourceBuilderEx".Equals(x.ConstructorArguments[0].Value))
            ?? false;
        if (!isLegacyResourcesClass)
            return null;

        var resourcePropertyName = methodName.Substring(0, methodName.Length - "Format".Length);

        return new ResourcesExContext(resourcePropertyName,
            resourcesClassExpr.WithoutLeadingTrivia(),
            invocationExpr.ArgumentList);
    }

    private static InvocationExpressionSyntax CreateUpdatedExpression(ResourcesExContext context)
    {
        var resourcePropertyExpr = IdentifierName(context.ResourcePropertyName);
        var resourceCultureExpr = IdentifierName("Culture");

        var resourceAccessor = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression, context.ResourcesClassExpr, resourcePropertyExpr);
        var resourceCultureAccessor = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression, context.ResourcesClassExpr, resourceCultureExpr);

        var stringFormatExpr = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                PredefinedType(
                    Token(SyntaxKind.StringKeyword)),
                IdentifierName("Format")));

        var stringFormatArguments = new List<SyntaxNodeOrToken> {
                        Argument(resourceCultureAccessor),
                        Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxTrivia(SyntaxKind.WhitespaceTrivia, " ")),
                        Argument(resourceAccessor),
                    };
        foreach (var originalArgument in context.ArgumentList.Arguments)
        {
            stringFormatArguments.Add(Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxTrivia(SyntaxKind.WhitespaceTrivia, " ")));
            stringFormatArguments.Add(originalArgument);
        }

        return stringFormatExpr.WithArgumentList(
            ArgumentList(SeparatedList<ArgumentSyntax>(stringFormatArguments)));
    }
}
