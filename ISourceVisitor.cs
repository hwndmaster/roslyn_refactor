using Microsoft.CodeAnalysis;

public interface ISourceVisitor
{
    Task<Document> VisitAsync(Document document);
}
