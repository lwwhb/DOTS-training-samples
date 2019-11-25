﻿using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Systems {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(UpdateTransformSystem))]
    public class UpdateIntersectionSystem : JobComponentSystem
    {
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            inputDeps.Complete();
            int frame = 1 + UnityEngine.Time.frameCount;
            Entities.ForEach((Entity entity, ref LocalIntersectionComponent localIntersection, ref OnSplineComponent onSpline, ref CarSpeedComponent speed, ref InIntersectionComponent inIntersection, in CoordinateSystemComponent coords) =>
            {
                if (inIntersection.Value)
                {
                    if (speed.SplineTimer < 1)
                        return;
                    // we're exiting an intersection - make sure the next road
                    // segment has room for us before we proceed
                    if (TrackSplines.GetQueue(onSpline.Spline, onSpline.Direction, onSpline.Side).Count <= TrackSplines.maxCarCount[onSpline.Spline])
                    {
                        Intersections.Occupied[localIntersection.Intersection][(localIntersection.Side + 1) / 2] = false;
                        inIntersection.Value = false;
                        speed.SplineTimer = 0f;
                    }
                    else
                    {
                        speed.SplineTimer = 1f;
                        speed.NormalizedSpeed = 0f;
                    }
                }
                else
                {
                    if (speed.SplineTimer < 1)
                    {
                        var queue = TrackSplines.GetQueue(onSpline.Spline, onSpline.Direction, onSpline.Side);
                        int idx = queue.FindIndex(entry => entry.Entity == entity);
                        var queueEntry = queue[idx];
                        queueEntry.SplineTimer = speed.SplineTimer;
                        queue[idx] = queueEntry;
                        return;
                    }
                    
                    // we're exiting a road segment - first, we need to know
                    // which intersection we're entering
                    ushort intersection;
                    if (onSpline.Direction == 1)
                    {
                        intersection = TrackSplines.endIntersection[onSpline.Spline];
                        localIntersection.Bezier.start = TrackSplines.bezier[onSpline.Spline].end;
                    }
                    else
                    {
                        intersection = TrackSplines.startIntersection[onSpline.Spline];
                        localIntersection.Bezier.start = TrackSplines.bezier[onSpline.Spline].start;
                    }

                    // now we need to know which road segment we'll move into
                    // (dead-ends force u-turns, but otherwise, u-turns are not allowed)
                    int newSplineIndex = 0;
                    if (Intersections.Neighbors[intersection].Count > 1)
                    {
                        int mySplineIndex = Intersections.NeighborSplines[intersection].IndexOf(onSpline.Spline);
                        newSplineIndex = new Random((uint)((entity.Index + 1) * frame * 47701)).NextInt(Intersections.NeighborSplines[intersection].Count - 1);
                        if (newSplineIndex >= mySplineIndex)
                        {
                            newSplineIndex++;
                        }
                    }

                    var newSpline = Intersections.NeighborSplines[intersection][newSplineIndex];

                    // make sure that our side of the intersection (top/bottom)
                    // is empty before we enter
                    if (Intersections.Occupied[intersection][(localIntersection.Side + 1) / 2])
                    {
                        speed.SplineTimer = 1f;
                        speed.NormalizedSpeed = 0f;
                    }
                    else
                    {
                        var previousLane = TrackSplines.GetQueue(onSpline.Spline, onSpline.Direction, onSpline.Side);

                        // to avoid flipping between top/bottom of our roads,
                        // we need to know our new spline's normal at our entrance point
                        float3 newNormal;
                        if (TrackSplines.startIntersection[newSpline] == intersection)
                        {
                            onSpline.Direction = 1;
                            newNormal = TrackSplines.geometry[newSpline].startNormal;
                            localIntersection.Bezier.end = TrackSplines.bezier[newSpline].start;
                        }
                        else
                        {
                            onSpline.Direction = -1;
                            newNormal = TrackSplines.geometry[newSpline].endNormal;
                            localIntersection.Bezier.end = TrackSplines.bezier[newSpline].end;
                        }

                        // now we'll prepare our intersection spline - this lets us
                        // create a "temporary lane" inside the current intersection
                        {
                            var pos = Intersections.Position[intersection];
                            var norm = Intersections.Normal[intersection];
                            localIntersection.Bezier.anchor1 = (pos + localIntersection.Bezier.start) * .5f;
                            localIntersection.Bezier.anchor2 = (pos + localIntersection.Bezier.end) * .5f;
                            localIntersection.Geometry.startTangent = math.round(math.normalize(pos - localIntersection.Bezier.start));
                            localIntersection.Geometry.endTangent = math.round(math.normalize(pos - localIntersection.Bezier.end));
                            localIntersection.Geometry.startNormal = norm;
                            localIntersection.Geometry.endNormal = norm;
                        }

                        if (onSpline.Spline == newSpline)
                        {
                            // u-turn - make our intersection spline more rounded than usual
                            float3 perp = math.cross(localIntersection.Geometry.startTangent, localIntersection.Geometry.startNormal);
                            localIntersection.Bezier.anchor1 += .5f * RoadGeneratorDots.intersectionSize * localIntersection.Geometry.startTangent;
                            localIntersection.Bezier.anchor2 += .5f * RoadGeneratorDots.intersectionSize * localIntersection.Geometry.startTangent;
                            localIntersection.Bezier.anchor1 -= localIntersection.Side * RoadGeneratorDots.trackRadius * .5f * perp;
                            localIntersection.Bezier.anchor2 += localIntersection.Side * RoadGeneratorDots.trackRadius * .5f * perp;
                        }

                        localIntersection.Intersection = intersection;
                        localIntersection.Length = localIntersection.Bezier.MeasureLength(RoadGeneratorDots.splineResolution);

                        inIntersection.Value = true;

                        // to maintain our current orientation, should we be
                        // on top of or underneath our next road segment?
                        // (each road segment has its own "up" direction, at each end)
                        onSpline.Side = (sbyte) (math.dot(newNormal, coords.Up) > 0f ? 1 : -1);

                        // should we be on top of or underneath the intersection?
                        localIntersection.Side = (sbyte) (math.dot(localIntersection.Geometry.startNormal, coords.Up) > 0f ? 1 : -1);

                        // block other cars from entering this intersection
                        Intersections.Occupied[intersection][(localIntersection.Side + 1) / 2] = true;

                        // remove ourselves from our previous lane's list of cars
                        previousLane.RemoveAll(entry => entry.Entity == entity);

                        // add "leftover" spline timer value to our new spline timer
                        // (avoids a stutter when changing between splines)
                        speed.SplineTimer = (speed.SplineTimer - 1f) * TrackSplines.measuredLength[onSpline.Spline] / localIntersection.Length;
                        onSpline.Spline = newSpline;

                        var queue = TrackSplines.GetQueue(onSpline.Spline, onSpline.Direction, onSpline.Side);
                        queue.Add(new QueueEntry
                        {
                            Entity = entity,
                            SplineTimer = speed.SplineTimer
                        });
                        Debug.Assert(queue.FindIndex(entry => entry.Entity == entity) >= 0);
                    }
                }
                
            }).WithoutBurst().Run();
            return default;
        }
    }
}
