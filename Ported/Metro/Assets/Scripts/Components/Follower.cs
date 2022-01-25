using System;
using Unity.Entities;

[GenerateAuthoringComponent]
public struct Follower : IComponentData
{
    public Entity Leader;
    public Entity TrackData;
    public int CartIndexInTrain;
}
