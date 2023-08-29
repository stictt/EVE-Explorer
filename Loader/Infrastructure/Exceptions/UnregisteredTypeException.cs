namespace Loader.Infrastructure.Exceptions
{
    public class UnregisteredTypeException : ArgumentException
    {
        public UnregisteredTypeException(string message) : base(message)
        {
        }
    }
}
