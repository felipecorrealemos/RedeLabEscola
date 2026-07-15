public enum ConveyorState
{
    Stopped,
    Starting,
    Running,
    Congested,
    Jammed,
    Restarting
}

public enum ConveyorItemState
{
    Moving,
    SlowingDown,
    WaitingForItem,
    WaitingForMachine,
    QueuedForCollection,
    BeingCollected,
    Removed
}

public enum ConveyorSpawnMode
{
    Sequential,
    RandomWeighted
}

public enum QueueDistributionMode
{
    Alternate,
    ShortestQueue,
    RandomAvailable
}
