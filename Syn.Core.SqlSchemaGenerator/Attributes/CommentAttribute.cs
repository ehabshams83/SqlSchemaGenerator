namespace Syn.Core.SqlSchemaGenerator.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class CommentAttribute : Attribute
    {
        public string Text { get; }
        public CommentAttribute(string text) => Text = text;
    }

}
