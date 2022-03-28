﻿using Unity.Entities;

namespace Components
{
    public struct TornadoSettings : IComponentData
    {
        public float tornadoForce;
        public float tornadoMaxForceDist;
        public float tornadoHeight;
        public float tornadoUpForce;
        public float tornadoInwardForce;
    }
}