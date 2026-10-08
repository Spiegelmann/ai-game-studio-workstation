namespace AiGameStudioWorkstation.Core.Domain.Policy;

public interface IPolicyEvaluator
{
    PolicyDecision Evaluate(PolicyAction action, PolicyContext context);
}
