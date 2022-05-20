using Unity.Entities;

public enum BucketInteractions {
    Pickup,
    PickedUp,
    Drop,
    Dropped,
    Pour
}

public struct Bucket : IComponentData {
    public float fillLevel;
    public Fetcher holder;

    public BucketInteractions Interactions;
}