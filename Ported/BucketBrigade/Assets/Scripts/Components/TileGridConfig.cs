using Unity.Entities;
using Unity.Mathematics;

struct TileGridConfig : IComponentData
{
    public int Size;
    public Entity TilePrefab;
    public float4 GrassColor;
    public float4 LightFireColor;
    public float4 MediumFireColor;
    public float4 IntenseFireColor;
}
