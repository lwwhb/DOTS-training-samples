using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct Config : IComponentData
{
    public const int Speed =1; // Will become dynamic

	// Simulation Constants
	public const int AntCount = 100;

	public const float MoveSpeed = 0.025f;

	public const float RotationAngle = 360f;
	// GreenDotDistance
	// SphereRadius
	// WallDistanceFromCenter
	// Total simulation size CellMapResolution 

}
