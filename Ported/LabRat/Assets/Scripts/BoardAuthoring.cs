﻿using Unity.Entities;
using UnityEngine;

[GenerateAuthoringComponent]
public struct Board : IComponentData
{
    public Entity tilePrefab;
    public Entity invisibleTilePrefab;
    public Entity wallPrefab;
    public Entity homeBasePrefab;
    public Entity ratPrefab;
    public Entity catPrefab;
    public Entity arrowPrefab;

    [Min(4)] public int size;
    public float yNoise;

    public int wallCount;
    public int holeCount;

    public int miceCount;
    public int miceWait;
    public int catCount;
    public int catWait;
}
