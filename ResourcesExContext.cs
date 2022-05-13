using Microsoft.CodeAnalysis.CSharp.Syntax;

record ResourcesExContext(string ResourcePropertyName, IdentifierNameSyntax ResourcesClassExpr, ArgumentListSyntax ArgumentList);
