namespace AiGameStudioWorkstation.Core.Domain.Policy;

public sealed record PolicyReason
{
    public PolicyReason(string code, string explanation)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A policy reason code must not be empty or whitespace.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(explanation))
        {
            throw new ArgumentException("A policy reason explanation must not be empty or whitespace.", nameof(explanation));
        }

        Code = code;
        Explanation = explanation;
    }

    public string Code { get; }

    public string Explanation { get; }
}
