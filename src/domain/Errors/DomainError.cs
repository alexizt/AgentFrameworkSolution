namespace AgentFrameworkSolution.Domain.Errors;

public abstract class DomainError : Exception
{
    public string Code { get; }

    protected DomainError(string message, string code) : base(message)
    {
        Code = code;
        HResult = -1;
    }
}
