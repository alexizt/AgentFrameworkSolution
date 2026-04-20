namespace AgentFrameworkSolution.Application.Errors;

public abstract class ApplicationError : Exception
{
    public string Code { get; }

    protected ApplicationError(string message, string code) : base(message)
    {
        Code = code;
    }
}
