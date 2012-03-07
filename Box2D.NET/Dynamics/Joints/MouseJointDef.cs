// ****************************************************************************
// Copyright (c) 2011, Daniel Murphy
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without modification,
// are permitted provided that the following conditions are met:
// * Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// * Redistributions in binary form must reproduce the above copyright notice,
// this list of conditions and the following disclaimer in the documentation
// and/or other materials provided with the distribution.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
// IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT,
// INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
// NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
// WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.
// ****************************************************************************

using Box2D.Common;

namespace Box2D.Dynamics.Joints
{

    /// <summary>
    /// Mouse joint definition. This requires a world target point, tuning parameters, and the time step.
    /// </summary>
    /// <author>Daniel</author>
    public class MouseJointDef : JointDef
    {
        /// <summary>
        /// The initial world target point. This is assumed to coincide with the body anchor initially.
        /// </summary>
        public readonly Vec2 target = new Vec2();

        /// <summary>
        /// The maximum constraint force that can be exerted to move the candidate body. Usually you will
        /// express as some multiple of the weight (multiplier * mass * gravity).
        /// </summary>
        public float maxForce;

        /// <summary>
        /// The response speed.
        /// </summary>
        public float frequencyHz;

        /// <summary>
        /// The damping ratio. 0 = no damping, 1 = critical damping.
        /// </summary>
        public float dampingRatio;

        public MouseJointDef()
        {
            Type = JointType.Mouse;
            target.Set(0, 0);
            maxForce = 0;
            frequencyHz = 5;
            dampingRatio = .7f;
        }
    }
}