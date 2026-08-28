using ShipmentTelemetry.Domain.Enums;

namespace ShipmentTelemetry.Domain.Aggregates;

public static class MilestoneTransitionRules
{
    private static readonly OperationalMilestone?[] ValidFlow =
    [
        OperationalMilestone.None,
        OperationalMilestone.ArrivedAtPort,
        OperationalMilestone.GateIn,
        OperationalMilestone.LoadedOnVessel,
        OperationalMilestone.DepartedPort,
        OperationalMilestone.GateOut
    ];

    public static bool CanTransition(OperationalMilestone current, OperationalMilestone target)
    {
        if (target == OperationalMilestone.None)
        {
            return false;
        }

        var currentIndex = Array.IndexOf(ValidFlow, current);
        var targetIndex = Array.IndexOf(ValidFlow, target);

        if (currentIndex < 0 || targetIndex < 0)
        {
            return false;
        }

        return targetIndex == currentIndex + 1;
    }
}
