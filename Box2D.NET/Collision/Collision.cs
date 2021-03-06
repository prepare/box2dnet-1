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

using System;
using System.Diagnostics;
using Box2D.Collision.Shapes;
using Box2D.Common;
using Box2D.Pooling;

namespace Box2D.Collision
{

    /// <summary>
    /// Functions used for computing contact points, distance queries, and TOI queries. Collision methods
    /// are non-static for pooling speed, retrieve a collision object from the {@link SingletonPool}.
    /// Should not be constructed.
    /// </summary>
    /// <author>Daniel Murphy</author>
    public class Collision
    {
        public static readonly int NULL_FEATURE = Int32.MaxValue;

        private readonly IWorldPool pool;

        public Collision(IWorldPool argPool)
        {
            incidentEdge[0] = new ClipVertex();
            incidentEdge[1] = new ClipVertex();
            clipPoints1[0] = new ClipVertex();
            clipPoints1[1] = new ClipVertex();
            clipPoints2[0] = new ClipVertex();
            clipPoints2[1] = new ClipVertex();
            pool = argPool;
        }

        private readonly DistanceInput input = new DistanceInput();
        private readonly Distance.SimplexCache cache = new Distance.SimplexCache();
        private readonly DistanceOutput output = new DistanceOutput();

        /// <summary>
        /// Determine if two generic shapes overlap.
        /// </summary>
        /// <param name="shapeA"></param>
        /// <param name="indexA"></param>
        /// <param name="shapeB"></param>
        /// <param name="indexB"></param>
        /// <param name="xfA"></param>
        /// <param name="xfB"></param>
        /// <returns></returns>
        public bool TestOverlap(Shape shapeA, int indexA, Shape shapeB, int indexB, Transform xfA, Transform xfB)
        {
            input.ProxyA.Set(shapeA, indexA);
            input.ProxyB.Set(shapeB, indexB);
            input.TransformA.Set(xfA);
            input.TransformB.Set(xfB);
            input.UseRadii = true;

            cache.Count = 0;

            pool.GetDistance().GetDistance(output, cache, input);
            // djm note: anything significant about 10.0f?
            return output.Distance < 10.0f * Settings.EPSILON;
        }

        /// <summary>
        /// Compute the point states given two manifolds. The states pertain to the transition from
        /// manifold1 to manifold2. So state1 is either persist or remove while state2 is either add or
        /// persist.
        /// </summary>
        /// <param name="state1"></param>
        /// <param name="state2"></param>
        /// <param name="manifold1"></param>
        /// <param name="manifold2"></param>
        public static void GetPointStates(PointState[] state1, PointState[] state2, Manifold manifold1, Manifold manifold2)
        {

            for (int i = 0; i < Settings.MAX_MANIFOLD_POINTS; i++)
            {
                state1[i] = PointState.NullState;
                state2[i] = PointState.NullState;
            }

            // Detect persists and removes.
            for (int i = 0; i < manifold1.PointCount; i++)
            {
                ContactID id = manifold1.Points[i].Id;

                state1[i] = PointState.RemoveState;

                for (int j = 0; j < manifold2.PointCount; j++)
                {
                    if (manifold2.Points[j].Id.IsEqual(id))
                    {
                        state1[i] = PointState.PersistState;
                        break;
                    }
                }
            }

            // Detect persists and adds
            for (int i = 0; i < manifold2.PointCount; i++)
            {
                ContactID id = manifold2.Points[i].Id;

                state2[i] = PointState.AddState;

                for (int j = 0; j < manifold1.PointCount; j++)
                {
                    if (manifold1.Points[j].Id.IsEqual(id))
                    {
                        state2[i] = PointState.PersistState;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Clipping for contact manifolds. Sutherland-Hodgman clipping.
        /// </summary>
        /// <param name="vOut"></param>
        /// <param name="vIn"></param>
        /// <param name="normal"></param>
        /// <param name="offset"></param>
        /// <param name="vertexIndexA"></param>
        /// <returns></returns>
        public static int ClipSegmentToLine(ClipVertex[] vOut, ClipVertex[] vIn, Vec2 normal, float offset, int vertexIndexA)
        {

            // Start with no output points
            int numOut = 0;

            // Calculate the distance of end points to the line
            float distance0 = Vec2.Dot(normal, vIn[0].V) - offset;
            float distance1 = Vec2.Dot(normal, vIn[1].V) - offset;

            // If the points are behind the plane
            if (distance0 <= 0.0f)
            {
                vOut[numOut++].Set(vIn[0]);
            }
            if (distance1 <= 0.0f)
            {
                vOut[numOut++].Set(vIn[1]);
            }

            // If the points are on different sides of the plane
            if (distance0 * distance1 < 0.0f)
            {
                // Find intersection point of edge and plane
                float interp = distance0 / (distance0 - distance1);
                // vOut[numOut].v = vIn[0].v + interp * (vIn[1].v - vIn[0].v);
                vOut[numOut].V.Set(vIn[1].V).SubLocal(vIn[0].V).MulLocal(interp).AddLocal(vIn[0].V);

                // VertexA is hitting edgeB.
                vOut[numOut].Id.IndexA = (sbyte)vertexIndexA;
                vOut[numOut].Id.IndexB = vIn[0].Id.IndexB;
                vOut[numOut].Id.TypeA = (sbyte)ContactID.Type.Vertex;
                vOut[numOut].Id.TypeB = (sbyte)ContactID.Type.Face;
                ++numOut;
            }

            return numOut;
        }

        // #### COLLISION STUFF (not from collision.h or collision.cpp) ####

        // djm pooling
        private static readonly Vec2 P_A = new Vec2();
        private static readonly Vec2 P_B = new Vec2();
        private static readonly Vec2 D = new Vec2();

        /// <summary>
        /// Compute the collision manifold between two circles.
        /// </summary>
        /// <param name="manifold"></param>
        /// <param name="circle1"></param>
        /// <param name="xfA"></param>
        /// <param name="circle2"></param>
        /// <param name="xfB"></param>
        public void CollideCircles(Manifold manifold, CircleShape circle1, Transform xfA, CircleShape circle2, Transform xfB)
        {
            manifold.PointCount = 0;

            // before inline:
            Transform.MulToOut(xfA, circle1.P, P_A);
            Transform.MulToOut(xfB, circle2.P, P_B);
            D.Set(P_B).SubLocal(P_A);
            float distSqr = D.X * D.X + D.Y * D.Y;

            // after inline:
            // final Vec2 v = circle1.m_p;
            // final float pAy = xfA.p.y + xfA.q.ex.y * v.x + xfA.q.ey.y * v.y;
            // final float pAx = xfA.p.x + xfA.q.ex.x * v.x + xfA.q.ey.x * v.y;
            //
            // final Vec2 v1 = circle2.m_p;
            // final float pBy = xfB.p.y + xfB.q.ex.y * v1.x + xfB.q.ey.y * v1.y;
            // final float pBx = xfB.p.x + xfB.q.ex.x * v1.x + xfB.q.ey.x * v1.y;
            //
            // final float dx = pBx - pAx;
            // final float dy = pBy - pAy;
            //
            // final float distSqr = dx * dx + dy * dy;
            // end inline

            float radius = circle1.Radius + circle2.Radius;
            if (distSqr > radius * radius)
            {
                return;
            }

            manifold.Type = Manifold.ManifoldType.Circles;
            manifold.LocalPoint.Set(circle1.P);
            manifold.LocalNormal.SetZero();
            manifold.PointCount = 1;

            manifold.Points[0].LocalPoint.Set(circle2.P);
            manifold.Points[0].Id.Zero();
        }

        // djm pooling, and from above
        private static readonly Vec2 C = new Vec2();
        private static readonly Vec2 C_LOCAL = new Vec2();

        /// <summary>
        /// Compute the collision manifold between a polygon and a circle.
        /// </summary>
        /// <param name="manifold"></param>
        /// <param name="polygon"></param>
        /// <param name="xfA"></param>
        /// <param name="circle"></param>
        /// <param name="xfB"></param>
        public void CollidePolygonAndCircle(Manifold manifold, PolygonShape polygon, Transform xfA, CircleShape circle, Transform xfB)
        {
            manifold.PointCount = 0;
            //Vec2 v = circle.m_p;

            // Compute circle position in the frame of the polygon.
            // before inline:
            Transform.MulToOut(xfB, circle.P, C);
            Transform.MulTransToOut(xfA, C, C_LOCAL);

            float cLocalx = C_LOCAL.X;
            float cLocaly = C_LOCAL.Y;
            // after inline:
            // final float cy = xfB.p.y + xfB.q.ex.y * v.x + xfB.q.ey.y * v.y;
            // final float cx = xfB.p.x + xfB.q.ex.x * v.x + xfB.q.ey.x * v.y;
            // final float v1x = cx - xfA.p.x;
            // final float v1y = cy - xfA.p.y;
            // final Vec2 b = xfA.q.ex;
            // final Vec2 b1 = xfA.q.ey;
            // final float cLocaly = v1x * b1.x + v1y * b1.y;
            // final float cLocalx = v1x * b.x + v1y * b.y;
            // end inline

            // Find the min separating edge.
            int normalIndex = 0;
            //UPGRADE_TODO: The equivalent in .NET for field 'java.lang.Float.MIN_VALUE' may return a different value. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1043'"
            float separation = Single.Epsilon;
            float radius = polygon.Radius + circle.Radius;
            int vertexCount = polygon.VertexCount;

            Vec2[] vertices = polygon.Vertices;
            Vec2[] normals = polygon.Normals;

            for (int i = 0; i < vertexCount; i++)
            {
                // before inline
                // temp.set(cLocal).subLocal(vertices[i]);
                // float s = Vec2.dot(normals[i], temp);
                // after inline
                Vec2 vertex = vertices[i];
                float tempx = cLocalx - vertex.X;
                float tempy = cLocaly - vertex.Y;
                Vec2 normal = normals[i];
                float s = normal.X * tempx + normal.Y * tempy;


                if (s > radius)
                {
                    // early out
                    return;
                }

                if (s > separation)
                {
                    separation = s;
                    normalIndex = i;
                }
            }

            // Vertices that subtend the incident face.
            int vertIndex1 = normalIndex;
            int vertIndex2 = vertIndex1 + 1 < vertexCount ? vertIndex1 + 1 : 0;
            Vec2 v1 = vertices[vertIndex1];
            Vec2 v2 = vertices[vertIndex2];

            // If the center is inside the polygon ...
            if (separation < Settings.EPSILON)
            {
                manifold.PointCount = 1;
                manifold.Type = Manifold.ManifoldType.FaceA;

                // before inline:
                // manifold.localNormal.set(normals[normalIndex]);
                // manifold.localPoint.set(v1).addLocal(v2).mulLocal(.5f);
                // manifold.points[0].localPoint.set(circle.m_p);
                // after inline:
                Vec2 normal = normals[normalIndex];
                manifold.LocalNormal.X = normal.X;
                manifold.LocalNormal.Y = normal.Y;
                manifold.LocalPoint.X = (v1.X + v2.X) * .5f;
                manifold.LocalPoint.Y = (v1.Y + v2.Y) * .5f;
                ManifoldPoint mpoint = manifold.Points[0];
                mpoint.LocalPoint.X = circle.P.X;
                mpoint.LocalPoint.Y = circle.P.Y;
                mpoint.Id.Zero();
                // end inline
                return;
            }

            // Compute barycentric coordinates
            // before inline:
            // temp.set(cLocal).subLocal(v1);
            // temp2.set(v2).subLocal(v1);
            // float u1 = Vec2.dot(temp, temp2);
            // temp.set(cLocal).subLocal(v2);
            // temp2.set(v1).subLocal(v2);
            // float u2 = Vec2.dot(temp, temp2);
            // after inline:
            float tempX = cLocalx - v1.X;
            float tempY = cLocaly - v1.Y;
            float temp2X = v2.X - v1.X;
            float temp2Y = v2.Y - v1.Y;
            float u1 = tempX * temp2X + tempY * temp2Y;

            float temp3X = cLocalx - v2.X;
            float temp3Y = cLocaly - v2.Y;
            float temp4X = v1.X - v2.X;
            float temp4Y = v1.Y - v2.Y;
            float u2 = temp3X * temp4X + temp3Y * temp4Y;
            // end inline

            if (u1 <= 0f)
            {
                // inlined
                float dx = cLocalx - v1.X;
                float dy = cLocaly - v1.Y;
                if (dx * dx + dy * dy > radius * radius)
                {
                    return;
                }

                manifold.PointCount = 1;
                manifold.Type = Manifold.ManifoldType.FaceA;
                // before inline:
                // manifold.localNormal.set(cLocal).subLocal(v1);
                // after inline:
                manifold.LocalNormal.X = cLocalx - v1.X;
                manifold.LocalNormal.Y = cLocaly - v1.Y;
                // end inline
                manifold.LocalNormal.Normalize();
                manifold.LocalPoint.Set(v1);
                manifold.Points[0].LocalPoint.Set(circle.P);
                manifold.Points[0].Id.Zero();
            }
            else if (u2 <= 0.0f)
            {
                // inlined
                float dx = cLocalx - v2.X;
                float dy = cLocaly - v2.Y;
                if (dx * dx + dy * dy > radius * radius)
                {
                    return;
                }

                manifold.PointCount = 1;
                manifold.Type = Manifold.ManifoldType.FaceA;
                // before inline:
                // manifold.localNormal.set(cLocal).subLocal(v2);
                // after inline:
                manifold.LocalNormal.X = cLocalx - v2.X;
                manifold.LocalNormal.Y = cLocaly - v2.Y;
                // end inline
                manifold.LocalNormal.Normalize();
                manifold.LocalPoint.Set(v2);
                manifold.Points[0].LocalPoint.Set(circle.P);
                manifold.Points[0].Id.Zero();
            }
            else
            {
                // Vec2 faceCenter = 0.5f * (v1 + v2);
                // (temp is faceCenter)
                // before inline:
                // temp.set(v1).addLocal(v2).mulLocal(.5f);
                //
                // temp2.set(cLocal).subLocal(temp);
                // separation = Vec2.dot(temp2, normals[vertIndex1]);
                // if (separation > radius) {
                // return;
                // }
                // after inline:
                float fcx = (v1.X + v2.X) * .5f;
                float fcy = (v1.Y + v2.Y) * .5f;

                float tx = cLocalx - fcx;
                float ty = cLocaly - fcy;
                Vec2 normal = normals[vertIndex1];
                separation = tx * normal.X + ty * normal.Y;
                if (separation > radius)
                {
                    return;
                }
                // end inline

                manifold.PointCount = 1;
                manifold.Type = Manifold.ManifoldType.FaceA;
                manifold.LocalNormal.Set(normals[vertIndex1]);
                manifold.LocalPoint.X = fcx; // (faceCenter)
                manifold.LocalPoint.Y = fcy;
                manifold.Points[0].LocalPoint.Set(circle.P);
                manifold.Points[0].Id.Zero();
            }
        }

        // djm pooling
        private readonly Vec2 normal1 = new Vec2();
        private readonly Vec2 normal1World = new Vec2();
        private readonly Vec2 v1 = new Vec2();
        private readonly Vec2 v2 = new Vec2();

        /// <summary> Find the separation between poly1 and poly2 for a given edge normal on poly1.
        /// 
        /// </summary>
        /// <param name="poly1">
        /// </param>
        /// <param name="xf1">
        /// </param>
        /// <param name="edge1">
        /// </param>
        /// <param name="poly2">
        /// </param>
        /// <param name="xf2">
        /// </param>
        public float EdgeSeparation(PolygonShape poly1, Transform xf1, int edge1, PolygonShape poly2, Transform xf2)
        {
            int count1;
            Vec2[] vertices1 = poly1.Vertices;
            Vec2[] normals1 = poly1.Normals;

            int count2 = poly2.VertexCount;
            Vec2[] vertices2 = poly2.Vertices;

            Debug.Assert(0 <= edge1 && edge1 < count1);
            // Convert normal from poly1's frame into poly2's frame.
            // before inline:
            // Vec2 normal1World = Mul(xf1.R, normals1[edge1]);
            Rot.MulToOutUnsafe(xf1.Q, normals1[edge1], normal1World);
            // Vec2 normal1 = MulT(xf2.R, normal1World);
            Rot.MulTransUnsafe(xf2.Q, normal1World, normal1);
            float normal1X = normal1.X;
            float normal1Y = normal1.Y;
            // after inline:
            // R.mulToOut(v,out);
            // final Mat22 R = xf1.q;
            // final Vec2 v = normals1[edge1];
            // final float normal1Worldy = R.ex.y * v.x + R.ey.y * v.y;
            // final float normal1Worldx = R.ex.x * v.x + R.ey.x * v.y;
            // final Mat22 R1 = xf2.q;
            // final float normal1x = normal1Worldx * R1.ex.x + normal1Worldy * R1.ex.y;
            // final float normal1y = normal1Worldx * R1.ey.x + normal1Worldy * R1.ey.y;
            // end inline

            // Find support vertex on poly2 for -normal.
            int index = 0;
            float minDot = Single.MaxValue;

            for (int i = 0; i < count2; ++i)
            {
                Vec2 a = vertices2[i];
                float dot = a.X * normal1X + a.Y * normal1Y;
                if (dot < minDot)
                {
                    minDot = dot;
                    index = i;
                }
            }

            // Vec2 v1 = Mul(xf1, vertices1[edge1]);
            // Vec2 v2 = Mul(xf2, vertices2[index]);
            // before inline:
            Transform.MulToOut(xf1, vertices1[edge1], v1);
            Transform.MulToOut(xf2, vertices2[index], v2);

            float separation = Vec2.Dot(v2.SubLocal(v1), normal1World);
            return separation;

            // after inline:
            // final Vec2 v3 = vertices1[edge1];
            // final float v1y = xf1.p.y + R.ex.y * v3.x + R.ey.y * v3.y;
            // final float v1x = xf1.p.x + R.ex.x * v3.x + R.ey.x * v3.y;
            // final Vec2 v4 = vertices2[index];
            // final float v2y = xf2.p.y + R1.ex.y * v4.x + R1.ey.y * v4.y - v1y;
            // final float v2x = xf2.p.x + R1.ex.x * v4.x + R1.ey.x * v4.y - v1x;
            //
            // return v2x * normal1Worldx + v2y * normal1Worldy;
            // end inline
        }

        // djm pooling, and from above
        private readonly Vec2 temp = new Vec2();
        private readonly Vec2 dLocal1 = new Vec2();

        /// <summary>
        /// Find the max separation between poly1 and poly2 using edge normals from poly1.
        /// </summary>
        /// <param name="results"></param>
        /// <param name="poly1"></param>
        /// <param name="xf1"></param>
        /// <param name="poly2"></param>
        /// <param name="xf2"></param>
        /// <returns></returns>
        public void FindMaxSeparation(EdgeResults results, PolygonShape poly1, Transform xf1, PolygonShape poly2, Transform xf2)
        {
            int count1 = poly1.VertexCount;
            Vec2[] normals1 = poly1.Normals;
            //Vec2 v = poly2.m_centroid;

            // Vector pointing from the centroid of poly1 to the centroid of poly2.
            // before inline:
            Transform.MulToOutUnsafe(xf2, poly2.Centroid, D);
            Transform.MulToOutUnsafe(xf1, poly1.Centroid, temp);
            D.SubLocal(temp);

            Rot.MulTransUnsafe(xf1.Q, D, dLocal1);
            float dLocal1X = dLocal1.X;
            float dLocal1Y = dLocal1.Y;
            // after inline:
            // final float predy = xf2.p.y + xf2.q.ex.y * v.x + xf2.q.ey.y * v.y;
            // final float predx = xf2.p.x + xf2.q.ex.x * v.x + xf2.q.ey.x * v.y;
            // final Vec2 v1 = poly1.m_centroid;
            // final float tempy = xf1.p.y + xf1.q.ex.y * v1.x + xf1.q.ey.y * v1.y;
            // final float tempx = xf1.p.x + xf1.q.ex.x * v1.x + xf1.q.ey.x * v1.y;
            // final float dx = predx - tempx;
            // final float dy = predy - tempy;
            //
            // final Mat22 R = xf1.q;
            // final float dLocal1x = dx * R.ex.x + dy * R.ex.y;
            // final float dLocal1y = dx * R.ey.x + dy * R.ey.y;
            // end inline

            // Find edge normal on poly1 that has the largest projection onto d.
            int edge = 0;
            //UPGRADE_TODO: The equivalent in .NET for field 'java.lang.Float.MIN_VALUE' may return a different value. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1043'"
            float maxDot = Single.Epsilon;
            for (int i = 0; i < count1; i++)
            {
                Vec2 normal = normals1[i];
                float dot = normal.X * dLocal1X + normal.Y * dLocal1Y;
                if (dot > maxDot)
                {
                    maxDot = dot;
                    edge = i;
                }
            }

            // Get the separation for the edge normal.
            float s = EdgeSeparation(poly1, xf1, edge, poly2, xf2);

            // Check the separation for the previous edge normal.
            int prevEdge = edge - 1 >= 0 ? edge - 1 : count1 - 1;
            float sPrev = EdgeSeparation(poly1, xf1, prevEdge, poly2, xf2);

            // Check the separation for the next edge normal.
            int nextEdge = edge + 1 < count1 ? edge + 1 : 0;
            float sNext = EdgeSeparation(poly1, xf1, nextEdge, poly2, xf2);

            // Find the best edge and the search direction.
            int bestEdge;
            float bestSeparation;
            int increment;
            if (sPrev > s && sPrev > sNext)
            {
                increment = -1;
                bestEdge = prevEdge;
                bestSeparation = sPrev;
            }
            else if (sNext > s)
            {
                increment = 1;
                bestEdge = nextEdge;
                bestSeparation = sNext;
            }
            else
            {
                results.EdgeIndex = edge;
                results.Separation = s;
                return;
            }

            // Perform a local search for the best edge normal.
            for (; ; )
            {
                if (increment == -1)
                {
                    edge = bestEdge - 1 >= 0 ? bestEdge - 1 : count1 - 1;
                }
                else
                {
                    edge = bestEdge + 1 < count1 ? bestEdge + 1 : 0;
                }

                s = EdgeSeparation(poly1, xf1, edge, poly2, xf2);

                if (s > bestSeparation)
                {
                    bestEdge = edge;
                    bestSeparation = s;
                }
                else
                {
                    break;
                }
            }

            results.EdgeIndex = bestEdge;
            results.Separation = bestSeparation;
        }

        // djm pooling from above
        public void FindIncidentEdge(ClipVertex[] c, PolygonShape poly1, Transform xf1, int edge1, PolygonShape poly2, Transform xf2)
        {
            int count1;
            Vec2[] normals1 = poly1.Normals;

            int count2 = poly2.VertexCount;
            Vec2[] vertices2 = poly2.Vertices;
            Vec2[] normals2 = poly2.Normals;

            Debug.Assert(0 <= edge1 && edge1 < count1);

            // Get the normal of the reference edge in poly2's frame.
            Rot.MulToOutUnsafe(xf1.Q, normals1[edge1], normal1); // temporary
            // Vec2 normal1 = MulT(xf2.R, Mul(xf1.R, normals1[edge1]));
            Rot.MulTrans(xf2.Q, normal1, normal1);

            // Find the incident edge on poly2.
            int index = 0;
            float minDot = Single.MaxValue;
            for (int i = 0; i < count2; ++i)
            {
                float dot = Vec2.Dot(normal1, normals2[i]);
                if (dot < minDot)
                {
                    minDot = dot;
                    index = i;
                }
            }

            // Build the clip vertices for the incident edge.
            int i1 = index;
            int i2 = i1 + 1 < count2 ? i1 + 1 : 0;

            Transform.MulToOutUnsafe(xf2, vertices2[i1], c[0].V); // = Mul(xf2, vertices2[i1]);
            c[0].Id.IndexA = (sbyte)edge1;
            c[0].Id.IndexB = (sbyte)i1;
            c[0].Id.TypeA = (sbyte)ContactID.Type.Face;
            c[0].Id.TypeB = (sbyte)ContactID.Type.Vertex;

            Transform.MulToOutUnsafe(xf2, vertices2[i2], c[1].V); // = Mul(xf2, vertices2[i2]);
            c[1].Id.IndexA = (sbyte)edge1;
            c[1].Id.IndexB = (sbyte)i2;
            c[1].Id.TypeA = (sbyte)ContactID.Type.Face;
            c[1].Id.TypeB = (sbyte)ContactID.Type.Vertex;
        }

        private readonly EdgeResults results1 = new EdgeResults();
        private readonly EdgeResults results2 = new EdgeResults();
        private readonly ClipVertex[] incidentEdge = new ClipVertex[2];
        private readonly Vec2 localTangent = new Vec2();
        private readonly Vec2 localNormal = new Vec2();
        private readonly Vec2 planePoint = new Vec2();
        private readonly Vec2 tangent = new Vec2();
        private readonly Vec2 normal = new Vec2();
        private readonly Vec2 v11 = new Vec2();
        private readonly Vec2 v12 = new Vec2();
        private readonly ClipVertex[] clipPoints1 = new ClipVertex[2];
        private readonly ClipVertex[] clipPoints2 = new ClipVertex[2];

        /// <summary>
        /// Compute the collision manifold between two polygons.
        /// </summary>
        /// <param name="manifold"></param>
        /// <param name="polyA"></param>
        /// <param name="xfA"></param>
        /// <param name="polyB"></param>
        /// <param name="xfB"></param>
        public void CollidePolygons(Manifold manifold, PolygonShape polyA, Transform xfA, PolygonShape polyB, Transform xfB)
        {
            // Find edge normal of max separation on A - return if separating axis is found
            // Find edge normal of max separation on B - return if separation axis is found
            // Choose reference edge as min(minA, minB)
            // Find incident edge
            // Clip

            // The normal points from 1 to 2

            manifold.PointCount = 0;
            float totalRadius = polyA.Radius + polyB.Radius;

            FindMaxSeparation(results1, polyA, xfA, polyB, xfB);
            if (results1.Separation > totalRadius)
            {
                return;
            }

            FindMaxSeparation(results2, polyB, xfB, polyA, xfA);
            if (results2.Separation > totalRadius)
            {
                return;
            }

            PolygonShape poly1; // reference polygon
            PolygonShape poly2; // incident polygon
            Transform xf1, xf2;
            int edge1; // reference edge
            bool flip;
            const float k_relativeTol = 0.98f;
            const float k_absoluteTol = 0.001f;

            if (results2.Separation > k_relativeTol * results1.Separation + k_absoluteTol)
            {
                poly1 = polyB;
                poly2 = polyA;
                xf1 = xfB;
                xf2 = xfA;
                edge1 = results2.EdgeIndex;
                manifold.Type = Manifold.ManifoldType.FaceB;
                flip = true;
            }
            else
            {
                poly1 = polyA;
                poly2 = polyB;
                xf1 = xfA;
                xf2 = xfB;
                edge1 = results1.EdgeIndex;
                manifold.Type = Manifold.ManifoldType.FaceA;
                flip = false;
            }

            FindIncidentEdge(incidentEdge, poly1, xf1, edge1, poly2, xf2);

            int count1 = poly1.VertexCount;
            Vec2[] vertices1 = poly1.Vertices;

            int iv1 = edge1;
            int iv2 = edge1 + 1 < count1 ? edge1 + 1 : 0;
            v11.Set(vertices1[iv1]);
            v12.Set(vertices1[iv2]);
            localTangent.Set(v12).SubLocal(v11);
            localTangent.Normalize();

            Vec2.CrossToOutUnsafe(localTangent, 1f, localNormal); // Vec2 localNormal = Vec2.cross(dv,
            // 1.0f);

            planePoint.Set(v11).AddLocal(v12).MulLocal(.5f); // Vec2 planePoint = 0.5f * (v11
            // + v12);

            Rot.MulToOutUnsafe(xf1.Q, localTangent, tangent); // Vec2 sideNormal = Mul(xf1.R, v12
            // - v11);
            Vec2.CrossToOutUnsafe(tangent, 1f, normal); // Vec2 frontNormal = Vec2.cross(sideNormal,
            // 1.0f);

            Transform.MulToOut(xf1, v11, v11);
            Transform.MulToOut(xf1, v12, v12);
            // v11 = Mul(xf1, v11);
            // v12 = Mul(xf1, v12);

            // Face offset
            float frontOffset = Vec2.Dot(normal, v11);

            // Side offsets, extended by polytope skin thickness.
            float sideOffset1 = -Vec2.Dot(tangent, v11) + totalRadius;
            float sideOffset2 = Vec2.Dot(tangent, v12) + totalRadius;

            // Clip incident edge against extruded edge1 side edges.
            // ClipVertex clipPoints1[2];
            // ClipVertex clipPoints2[2];
            int np;

            // Clip to box side 1
            // np = ClipSegmentToLine(clipPoints1, incidentEdge, -sideNormal, sideOffset1);
            tangent.NegateLocal();
            np = ClipSegmentToLine(clipPoints1, incidentEdge, tangent, sideOffset1, iv1);
            tangent.NegateLocal();

            if (np < 2)
            {
                return;
            }

            // Clip to negative box side 1
            np = ClipSegmentToLine(clipPoints2, clipPoints1, tangent, sideOffset2, iv2);

            if (np < 2)
            {
                return;
            }

            // Now clipPoints2 contains the clipped points.
            manifold.LocalNormal.Set(localNormal);
            manifold.LocalPoint.Set(planePoint);

            int pointCount = 0;
            for (int i = 0; i < Settings.MAX_MANIFOLD_POINTS; ++i)
            {
                float separation = Vec2.Dot(normal, clipPoints2[i].V) - frontOffset;

                if (separation <= totalRadius)
                {
                    ManifoldPoint cp = manifold.Points[pointCount];
                    Transform.MulTransToOut(xf2, clipPoints2[i].V, cp.LocalPoint);
                    // cp.m_localPoint = MulT(xf2, clipPoints2[i].v);
                    cp.Id.Set(clipPoints2[i].Id);
                    if (flip)
                    {
                        // Swap features
                        cp.Id.Flip();
                    }
                    ++pointCount;
                }
            }

            manifold.PointCount = pointCount;
        }


        private readonly Vec2 q = new Vec2();
        private readonly Vec2 e = new Vec2();
        private readonly ContactID cf = new ContactID();
        private readonly Vec2 e1 = new Vec2();
        private readonly Vec2 p = new Vec2();
        private readonly Vec2 n = new Vec2();

        // Compute contact points for edge versus circle.
        // This accounts for edge connectivity.
        public void CollideEdgeAndCircle(Manifold manifold, EdgeShape edgeA, Transform xfA, CircleShape circleB, Transform xfB)
        {
            manifold.PointCount = 0;


            // Compute circle in frame of edge
            // Vec2 Q = MulT(xfA, Mul(xfB, circleB.m_p));
            Transform.MulToOutUnsafe(xfB, circleB.P, temp);
            Transform.MulTransToOutUnsafe(xfA, temp, q);

            Vec2 A = edgeA.Vertex1;
            Vec2 B = edgeA.Vertex2;
            e.Set(B).SubLocal(A);

            // Barycentric coordinates
            float u = Vec2.Dot(e, temp.Set(B).SubLocal(q));
            float v = Vec2.Dot(e, temp.Set(q).SubLocal(A));

            float radius = edgeA.Radius + circleB.Radius;

            // ContactFeature cf;
            cf.IndexB = 0;
            cf.TypeB = (sbyte)ContactID.Type.Vertex;

            // Region A
            if (v <= 0.0f)
            {
                Vec2 P = A;
                D.Set(q).SubLocal(P);
                float dd = Vec2.Dot(D, D);
                if (dd > radius * radius)
                {
                    return;
                }

                // Is there an edge connected to A?
                if (edgeA.HasVertex0)
                {
                    Vec2 A1 = edgeA.Vertex0;
                    Vec2 B1 = A;
                    e1.Set(B1).SubLocal(A1);
                    float u1 = Vec2.Dot(e1, temp.Set(B1).SubLocal(q));

                    // Is the circle in Region AB of the previous edge?
                    if (u1 > 0.0f)
                    {
                        return;
                    }
                }

                cf.IndexA = 0;
                cf.TypeA = (sbyte)ContactID.Type.Vertex;
                manifold.PointCount = 1;
                manifold.Type = Manifold.ManifoldType.Circles;
                manifold.LocalNormal.SetZero();
                manifold.LocalPoint.Set(P);
                // manifold.points[0].id.key = 0;
                manifold.Points[0].Id.Set(cf);
                manifold.Points[0].LocalPoint.Set(circleB.P);
                return;
            }

            // Region B
            if (u <= 0.0f)
            {
                Vec2 P = B;
                D.Set(q).SubLocal(P);
                float dd = Vec2.Dot(D, D);
                if (dd > radius * radius)
                {
                    return;
                }

                // Is there an edge connected to B?
                if (edgeA.HasVertex3)
                {
                    Vec2 B2 = edgeA.Vertex3;
                    Vec2 A2 = B;
                    Vec2 e2 = e1;
                    e2.Set(B2).SubLocal(A2);
                    float v2 = Vec2.Dot(e2, temp.Set(q).SubLocal(A2));

                    // Is the circle in Region AB of the next edge?
                    if (v2 > 0.0f)
                    {
                        return;
                    }
                }

                cf.IndexA = 1;
                cf.TypeA = (sbyte)ContactID.Type.Vertex;
                manifold.PointCount = 1;
                manifold.Type = Manifold.ManifoldType.Circles;
                manifold.LocalNormal.SetZero();
                manifold.LocalPoint.Set(P);
                // manifold.points[0].id.key = 0;
                manifold.Points[0].Id.Set(cf);
                manifold.Points[0].LocalPoint.Set(circleB.P);
                return;
            }

            // Region AB
            float den = Vec2.Dot(e, e);
            Debug.Assert(den > 0.0f);

            // Vec2 P = (1.0f / den) * (u * A + v * B);
            p.Set(A).MulLocal(u).AddLocal(temp.Set(B).MulLocal(v));
            p.MulLocal(1.0f / den);
            D.Set(q).SubLocal(p);
            float dd2 = Vec2.Dot(D, D);
            if (dd2 > radius * radius)
            {
                return;
            }

            n.X = -e.Y;
            n.Y = e.X;
            if (Vec2.Dot(n, temp.Set(q).SubLocal(A)) < 0.0f)
            {
                n.Set(-n.X, -n.Y);
            }
            n.Normalize();

            cf.IndexA = 0;
            cf.TypeA = (sbyte)ContactID.Type.Face;
            manifold.PointCount = 1;
            manifold.Type = Manifold.ManifoldType.FaceA;
            manifold.LocalNormal.Set(n);
            manifold.LocalPoint.Set(A);
            // manifold.points[0].id.key = 0;
            manifold.Points[0].Id.Set(cf);
            manifold.Points[0].LocalPoint.Set(circleB.P);
        }

        private readonly EPCollider collider = new EPCollider();

        public void CollideEdgeAndPolygon(Manifold manifold, EdgeShape edgeA, Transform xfA, PolygonShape polygonB, Transform xfB)
        {
            collider.Collide(manifold, edgeA, xfA, polygonB, xfB);
        }

        /// <summary>
        ///  Java-specific class for returning edge results
        /// </summary>
        public class EdgeResults
        {
            public float Separation;
            public int EdgeIndex;
        }

        /// <summary>
        ///  Used for computing contact manifolds.
        /// </summary>
        public class ClipVertex
        {
            public readonly Vec2 V;
            public readonly ContactID Id;

            public ClipVertex()
            {
                V = new Vec2();
                Id = new ContactID();
            }

            public void Set(ClipVertex cv)
            {
                V.Set(cv.V);
                Id.Set(cv.Id);
            }
        }

        /// <summary>
        /// This is used for determining the state of contact points.
        /// </summary>
        /// <author>Daniel Murphy</author>
        public enum PointState
        {
            /// <summary> point does not exist</summary>
            NullState,

            /// <summary> point was added in the update</summary>
            AddState,

            /// <summary> point persisted across the update</summary>
            PersistState,

            /// <summary> point was removed in the update</summary>
            RemoveState
        }

        /// <summary>
        ///  This structure is used to keep track of the best separating axis.
        /// </summary>
        //UPGRADE_NOTE: The access modifier for this class or class field has been changed in order to prevent compilation errors due to the visibility level. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1296'"
        private class EPAxis
        {
            internal enum EPAxisType
            {
                Unknown,
                EdgeA,
                EdgeB
            }

            internal EPAxisType Type;
            internal int Index;
            internal float Separation;
        }

        /// <summary>
        /// This holds polygon B expressed in frame A.
        /// </summary>
        private class TempPolygon
        {
            internal Vec2[] Vertices = new Vec2[Settings.MAX_POLYGON_VERTICES];
            internal Vec2[] Normals = new Vec2[Settings.MAX_POLYGON_VERTICES];
            internal int Count;

            public TempPolygon()
            {
                for (int i = 0; i < Vertices.Length; i++)
                {
                    Vertices[i] = new Vec2();
                    Normals[i] = new Vec2();
                }
            }
        }

        /// <summary>
        ///  Reference face used for clipping
        /// </summary>
        private class ReferenceFace
        {
            internal int I1;
            internal int I2;
            internal readonly Vec2 V1 = new Vec2();
            internal readonly Vec2 V2 = new Vec2();
            internal readonly Vec2 Normal = new Vec2();

            internal readonly Vec2 SideNormal1 = new Vec2();
            internal float SideOffset1;

            internal readonly Vec2 SideNormal2 = new Vec2();
            internal float SideOffset2;
        }

        /// <summary> 
        /// This class collides and edge and a polygon, taking into account edge adjacency.
        /// </summary>
        private class EPCollider
        {
            internal enum VertexType
            {
                Isolated,
                Concave,
                Convex
            }

            private readonly TempPolygon polygonB = new TempPolygon();

            private readonly Transform xf = new Transform();
            private readonly Vec2 centroidB = new Vec2();
            private Vec2 v0 = new Vec2();
            private Vec2 v1 = new Vec2();
            private Vec2 v2 = new Vec2();
            private Vec2 v3 = new Vec2();
            private readonly Vec2 normal0 = new Vec2();
            private readonly Vec2 normal1 = new Vec2();
            private readonly Vec2 normal2 = new Vec2();
            private readonly Vec2 normal = new Vec2();

            private readonly Vec2 lowerLimit = new Vec2();
            private readonly Vec2 upperLimit = new Vec2();
            private float radius;
            private bool front;

            public EPCollider()
            {
                for (int i = 0; i < 2; i++)
                {
                    ie[i] = new ClipVertex();
                    clipPoints1[i] = new ClipVertex();
                    clipPoints2[i] = new ClipVertex();
                }
            }

            private readonly Vec2 edge1 = new Vec2();
            private readonly Vec2 temp = new Vec2();
            private readonly Vec2 edge0 = new Vec2();
            private readonly Vec2 edge2 = new Vec2();
            private readonly ClipVertex[] ie = new ClipVertex[2];
            private readonly ClipVertex[] clipPoints1 = new ClipVertex[2];
            private readonly ClipVertex[] clipPoints2 = new ClipVertex[2];
            private readonly ReferenceFace rf = new ReferenceFace();
            private readonly EPAxis edgeAxis = new EPAxis();
            private readonly EPAxis polygonAxis = new EPAxis();

            public void Collide(Manifold manifold, EdgeShape edgeA, Transform xfA, PolygonShape polygonB, Transform xfB)
            {

                Transform.MulTransToOutUnsafe(xfA, xfB, xf);
                Transform.MulToOutUnsafe(xf, polygonB.Centroid, centroidB);

                v0 = edgeA.Vertex0;
                v1 = edgeA.Vertex1;
                v2 = edgeA.Vertex2;
                v3 = edgeA.Vertex3;

                bool hasVertex0 = edgeA.HasVertex0;
                bool hasVertex3 = edgeA.HasVertex3;

                edge1.Set(v2).SubLocal(v1);
                edge1.Normalize();
                normal1.Set(edge1.Y, -edge1.X);
                float offset1 = Vec2.Dot(normal1, temp.Set(centroidB).SubLocal(v1));
                float offset0 = 0.0f, offset2 = 0.0f;
                bool convex1 = false, convex2 = false;

                // Is there a preceding edge?
                if (hasVertex0)
                {
                    edge0.Set(v1).SubLocal(v0);
                    edge0.Normalize();
                    normal0.Set(edge0.Y, -edge0.X);
                    convex1 = Vec2.Cross(edge0, edge1) >= 0.0f;
                    offset0 = Vec2.Dot(normal0, temp.Set(centroidB).SubLocal(v0));
                }

                // Is there a following edge?
                if (hasVertex3)
                {
                    edge2.Set(v3).SubLocal(v2);
                    edge2.Normalize();
                    normal2.Set(edge2.Y, -edge2.X);
                    convex2 = Vec2.Cross(edge1, edge2) > 0.0f;
                    offset2 = Vec2.Dot(normal2, temp.Set(centroidB).SubLocal(v2));
                }

                // Determine front or back collision. Determine collision normal limits.
                if (hasVertex0 && hasVertex3)
                {
                    if (convex1 && convex2)
                    {
                        front = offset0 >= 0.0f || offset1 >= 0.0f || offset2 >= 0.0f;
                        if (front)
                        {
                            normal.Set(normal1);
                            lowerLimit.Set(normal0);
                            upperLimit.Set(normal2);
                        }
                        else
                        {
                            normal.Set(normal1).NegateLocal();
                            lowerLimit.Set(normal1).NegateLocal();
                            upperLimit.Set(normal1).NegateLocal();
                        }
                    }
                    else if (convex1)
                    {
                        front = offset0 >= 0.0f || (offset1 >= 0.0f && offset2 >= 0.0f);
                        if (front)
                        {
                            normal.Set(normal1);
                            lowerLimit.Set(normal0);
                            upperLimit.Set(normal1);
                        }
                        else
                        {
                            normal.Set(normal1).NegateLocal();
                            lowerLimit.Set(normal2).NegateLocal();
                            upperLimit.Set(normal1).NegateLocal();
                        }
                    }
                    else if (convex2)
                    {
                        front = offset2 >= 0.0f || (offset0 >= 0.0f && offset1 >= 0.0f);
                        if (front)
                        {
                            normal.Set(normal1);
                            lowerLimit.Set(normal1);
                            upperLimit.Set(normal2);
                        }
                        else
                        {
                            normal.Set(normal1).NegateLocal();
                            lowerLimit.Set(normal1).NegateLocal();
                            upperLimit.Set(normal0).NegateLocal();
                        }
                    }
                    else
                    {
                        front = offset0 >= 0.0f && offset1 >= 0.0f && offset2 >= 0.0f;
                        if (front)
                        {
                            normal.Set(normal1);
                            lowerLimit.Set(normal1);
                            upperLimit.Set(normal1);
                        }
                        else
                        {
                            normal.Set(normal1).NegateLocal();
                            lowerLimit.Set(normal2).NegateLocal();
                            upperLimit.Set(normal0).NegateLocal();
                        }
                    }
                }
                else if (hasVertex0)
                {
                    if (convex1)
                    {
                        front = offset0 >= 0.0f || offset1 >= 0.0f;
                        if (front)
                        {
                            normal.Set(normal1);
                            lowerLimit.Set(normal0);
                            upperLimit.Set(normal1).NegateLocal();
                        }
                        else
                        {
                            normal.Set(normal1).NegateLocal();
                            lowerLimit.Set(normal1);
                            upperLimit.Set(normal1).NegateLocal();
                        }
                    }
                    else
                    {
                        front = offset0 >= 0.0f && offset1 >= 0.0f;
                        if (front)
                        {
                            normal.Set(normal1);
                            lowerLimit.Set(normal1);
                            upperLimit.Set(normal1).NegateLocal();
                        }
                        else
                        {
                            normal.Set(normal1).NegateLocal();
                            lowerLimit.Set(normal1);
                            upperLimit.Set(normal0).NegateLocal();
                        }
                    }
                }
                else if (hasVertex3)
                {
                    if (convex2)
                    {
                        front = offset1 >= 0.0f || offset2 >= 0.0f;
                        if (front)
                        {
                            normal.Set(normal1);
                            lowerLimit.Set(normal1).NegateLocal();
                            upperLimit.Set(normal2);
                        }
                        else
                        {
                            normal.Set(normal1).NegateLocal();
                            lowerLimit.Set(normal1).NegateLocal();
                            upperLimit.Set(normal1);
                        }
                    }
                    else
                    {
                        front = offset1 >= 0.0f && offset2 >= 0.0f;
                        if (front)
                        {
                            normal.Set(normal1);
                            lowerLimit.Set(normal1).NegateLocal();
                            upperLimit.Set(normal1);
                        }
                        else
                        {
                            normal.Set(normal1).NegateLocal();
                            lowerLimit.Set(normal2).NegateLocal();
                            upperLimit.Set(normal1);
                        }
                    }
                }
                else
                {
                    front = offset1 >= 0.0f;
                    if (front)
                    {
                        normal.Set(normal1);
                        lowerLimit.Set(normal1).NegateLocal();
                        upperLimit.Set(normal1).NegateLocal();
                    }
                    else
                    {
                        normal.Set(normal1).NegateLocal();
                        lowerLimit.Set(normal1);
                        upperLimit.Set(normal1);
                    }
                }

                // Get polygonB in frameA
                this.polygonB.Count = polygonB.VertexCount;
                for (int i = 0; i < polygonB.VertexCount; ++i)
                {
                    Transform.MulToOutUnsafe(xf, polygonB.Vertices[i], this.polygonB.Vertices[i]);
                    Rot.MulToOutUnsafe(xf.Q, polygonB.Normals[i], this.polygonB.Normals[i]);
                }

                radius = 2.0f * Settings.POLYGON_RADIUS;

                manifold.PointCount = 0;

                ComputeEdgeSeparation(edgeAxis);

                // If no valid normal can be found than this edge should not collide.
                if (edgeAxis.Type == EPAxis.EPAxisType.Unknown)
                {
                    return;
                }

                if (edgeAxis.Separation > radius)
                {
                    return;
                }

                ComputePolygonSeparation(polygonAxis);
                if (polygonAxis.Type != EPAxis.EPAxisType.Unknown && polygonAxis.Separation > radius)
                {
                    return;
                }

                // Use hysteresis for jitter reduction.
                const float k_relativeTol = 0.98f;
                const float k_absoluteTol = 0.001f;

                EPAxis primaryAxis;
                if (polygonAxis.Type == EPAxis.EPAxisType.Unknown)
                {
                    primaryAxis = edgeAxis;
                }
                else if (polygonAxis.Separation > k_relativeTol * edgeAxis.Separation + k_absoluteTol)
                {
                    primaryAxis = polygonAxis;
                }
                else
                {
                    primaryAxis = edgeAxis;
                }

                // ClipVertex[] ie = new ClipVertex[2];
                if (primaryAxis.Type == EPAxis.EPAxisType.EdgeA)
                {
                    manifold.Type = Manifold.ManifoldType.FaceA;

                    // Search for the polygon normal that is most anti-parallel to the edge normal.
                    int bestIndex = 0;
                    float bestValue = Vec2.Dot(normal, this.polygonB.Normals[0]);
                    for (int i = 1; i < this.polygonB.Count; ++i)
                    {
                        float value = Vec2.Dot(normal, this.polygonB.Normals[i]);
                        if (value < bestValue)
                        {
                            bestValue = value;
                            bestIndex = i;
                        }
                    }

                    int i1 = bestIndex;
                    int i2 = i1 + 1 < this.polygonB.Count ? i1 + 1 : 0;

                    ie[0].V.Set(this.polygonB.Vertices[i1]);
                    ie[0].Id.IndexA = 0;
                    ie[0].Id.IndexB = (sbyte)i1;
                    ie[0].Id.TypeA = (sbyte)ContactID.Type.Face;
                    ie[0].Id.TypeB = (sbyte)ContactID.Type.Vertex;

                    ie[1].V.Set(this.polygonB.Vertices[i2]);
                    ie[1].Id.IndexA = 0;
                    ie[1].Id.IndexB = (sbyte)i2;
                    ie[1].Id.TypeA = (sbyte)ContactID.Type.Face;
                    ie[1].Id.TypeB = (sbyte)ContactID.Type.Vertex;

                    if (front)
                    {
                        rf.I1 = 0;
                        rf.I2 = 1;
                        rf.V1.Set(v1);
                        rf.V2.Set(v2);
                        rf.Normal.Set(normal1);
                    }
                    else
                    {
                        rf.I1 = 1;
                        rf.I2 = 0;
                        rf.V1.Set(v2);
                        rf.V2.Set(v1);
                        rf.Normal.Set(normal1).NegateLocal();
                    }
                }
                else
                {
                    manifold.Type = Manifold.ManifoldType.FaceB;

                    ie[0].V.Set(v1);
                    ie[0].Id.IndexA = 0;
                    ie[0].Id.IndexB = (sbyte)primaryAxis.Index;
                    ie[0].Id.TypeA = (sbyte)ContactID.Type.Vertex;
                    ie[0].Id.TypeB = (sbyte)ContactID.Type.Face;

                    ie[1].V.Set(v2);
                    ie[1].Id.IndexA = 0;
                    ie[1].Id.IndexB = (sbyte)primaryAxis.Index;
                    ie[1].Id.TypeA = (sbyte)ContactID.Type.Vertex;
                    ie[1].Id.TypeB = (sbyte)ContactID.Type.Face;

                    rf.I1 = primaryAxis.Index;
                    rf.I2 = rf.I1 + 1 < this.polygonB.Count ? rf.I1 + 1 : 0;
                    rf.V1.Set(this.polygonB.Vertices[rf.I1]);
                    rf.V2.Set(this.polygonB.Vertices[rf.I2]);
                    rf.Normal.Set(this.polygonB.Normals[rf.I1]);
                }

                rf.SideNormal1.Set(rf.Normal.Y, -rf.Normal.X);
                rf.SideNormal2.Set(rf.SideNormal1).NegateLocal();
                rf.SideOffset1 = Vec2.Dot(rf.SideNormal1, rf.V1);
                rf.SideOffset2 = Vec2.Dot(rf.SideNormal2, rf.V2);

                // Clip incident edge against extruded edge1 side edges.
                int np;

                // Clip to box side 1
                np = ClipSegmentToLine(clipPoints1, ie, rf.SideNormal1, rf.SideOffset1, rf.I1);

                if (np < Settings.MAX_MANIFOLD_POINTS)
                {
                    return;
                }

                // Clip to negative box side 1
                np = ClipSegmentToLine(clipPoints2, clipPoints1, rf.SideNormal2, rf.SideOffset2, rf.I2);

                if (np < Settings.MAX_MANIFOLD_POINTS)
                {
                    return;
                }

                // Now clipPoints2 contains the clipped points.
                if (primaryAxis.Type == EPAxis.EPAxisType.EdgeA)
                {
                    manifold.LocalNormal.Set(rf.Normal);
                    manifold.LocalPoint.Set(rf.V1);
                }
                else
                {
                    manifold.LocalNormal.Set(polygonB.Normals[rf.I1]);
                    manifold.LocalPoint.Set(polygonB.Vertices[rf.I1]);
                }

                int pointCount = 0;
                for (int i = 0; i < Settings.MAX_MANIFOLD_POINTS; ++i)
                {
                    float separation = Vec2.Dot(rf.Normal, temp.Set(clipPoints2[i].V).SubLocal(rf.V1));

                    if (separation <= radius)
                    {
                        ManifoldPoint cp = manifold.Points[pointCount];

                        if (primaryAxis.Type == EPAxis.EPAxisType.EdgeA)
                        {
                            // cp.localPoint = MulT(m_xf, clipPoints2[i].v);
                            Transform.MulTransToOutUnsafe(xf, clipPoints2[i].V, cp.LocalPoint);
                            cp.Id.Set(clipPoints2[i].Id);
                        }
                        else
                        {
                            cp.LocalPoint.Set(clipPoints2[i].V);
                            cp.Id.TypeA = clipPoints2[i].Id.TypeB;
                            cp.Id.TypeB = clipPoints2[i].Id.TypeA;
                            cp.Id.IndexA = clipPoints2[i].Id.IndexB;
                            cp.Id.IndexB = clipPoints2[i].Id.IndexA;
                        }

                        ++pointCount;
                    }
                }

                manifold.PointCount = pointCount;
            }


            private void ComputeEdgeSeparation(EPAxis axis)
            {
                axis.Type = EPAxis.EPAxisType.EdgeA;
                axis.Index = front ? 0 : 1;
                axis.Separation = System.Single.MaxValue;

                for (int i = 0; i < polygonB.Count; ++i)
                {
                    float s = Vec2.Dot(normal, temp.Set(polygonB.Vertices[i]).SubLocal(v1));
                    if (s < axis.Separation)
                    {
                        axis.Separation = s;
                    }
                }
            }

            private readonly Vec2 perp = new Vec2();
            private readonly Vec2 n = new Vec2();

            private void ComputePolygonSeparation(EPAxis axis)
            {
                axis.Type = EPAxis.EPAxisType.Unknown;
                axis.Index = -1;
                //UPGRADE_TODO: The equivalent in .NET for field 'java.lang.Float.MIN_VALUE' may return a different value. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1043'"
                axis.Separation = Single.Epsilon;

                perp.Set(-normal.Y, normal.X);

                for (int i = 0; i < polygonB.Count; ++i)
                {
                    n.Set(polygonB.Normals[i]).NegateLocal();

                    float s1 = Vec2.Dot(n, temp.Set(polygonB.Vertices[i]).SubLocal(v1));
                    float s2 = Vec2.Dot(n, temp.Set(polygonB.Vertices[i]).SubLocal(v2));
                    float s = MathUtils.Min(s1, s2);

                    if (s > radius)
                    {
                        // No collision
                        axis.Type = EPAxis.EPAxisType.EdgeB;
                        axis.Index = i;
                        axis.Separation = s;
                        return;
                    }

                    // Adjacency
                    if (Vec2.Dot(n, perp) >= 0.0f)
                    {
                        if (Vec2.Dot(temp.Set(n).SubLocal(upperLimit), normal) < -Settings.ANGULAR_SLOP)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        if (Vec2.Dot(temp.Set(n).SubLocal(lowerLimit), normal) < -Settings.ANGULAR_SLOP)
                        {
                            continue;
                        }
                    }

                    if (s > axis.Separation)
                    {
                        axis.Type = EPAxis.EPAxisType.EdgeB;
                        axis.Index = i;
                        axis.Separation = s;
                    }
                }
            }
        }
    }
}