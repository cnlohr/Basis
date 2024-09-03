﻿using System;
using UnityEngine;
using Gizmos = Popcron.Gizmos;
namespace JigglePhysics
{
    [Serializable]
    public class JiggleZone : JiggleRig
    {
        [Tooltip("How large of a radius the zone should effect, in target-space meters. (Scaling the target will effect the radius.)")]
        public float radius;
        // Constructor using arrays instead of ICollection
        public JiggleZone(Transform rootTransform, JiggleSettingsBase jiggleSettings, Transform[] ignoredTransforms, Collider[] colliders) : base(rootTransform, jiggleSettings, ignoredTransforms, colliders) { }
        // Modified method using arrays instead of ICollection
        protected override void CreateSimulatedPoints(ref JiggleBone[] outputPoints, Transform[] ignoredTransforms, Transform currentTransform, JiggleBone parentJiggleBone)
        {
            var parent = new JiggleBone(currentTransform, null);
            // Ensure the array has enough size before assigning values
            if (outputPoints.Length > 1)
            {
                outputPoints[0] = parent;
                outputPoints[1] = new JiggleBone(null, parent, 0f);
            }
            else
            {
                Debug.LogError("The outputPoints array does not have enough size.");
            }
        }
        public void DebugDraw()
        {
            Debug.DrawLine(GetPointSolve(), GetRootTransform().position, Color.cyan, 0, false);
        }
        public Vector3 GetPointSolve() => simulatedPoints[1].GetCachedSolvePosition();
        public new void OnRenderObject()
        {
            if (GetRootTransform() == null)
            {
                return;
            }
            Gizmos.Sphere(GetRootTransform().position, radius * GetRootTransform().lossyScale.x, new Color(0.1f, 0.1f, 0.8f, 0.5f));
        }
    }
}