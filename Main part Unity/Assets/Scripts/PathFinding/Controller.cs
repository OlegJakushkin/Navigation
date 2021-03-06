﻿using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Core;
using JetBrains.Annotations;
using UnityEngine;
using System;
using System.Text;

namespace Assets.Scripts.PathFinding {
    public class Controller : MonoBehaviour {

        public DebugManager DebugManagerAStar;

        [UsedImplicitly]
        public Vector3 From;

        [UsedImplicitly]
        public Vector3 To;
        
        public const float distanceStraight = 3f;
        public const float distanceDiagonal = 4.2426406871f;
        public const int mapHeight = 34;
        public const int mapWidth = 35;
        public Node[,] NodesArray = new Node [mapHeight,mapWidth];
		public bool IsPrecomputed;
        public List<Node> JumpPoints = new List<Node>();
        public List<BoundingBoxes> Boxes = new List<BoundingBoxes>();
        
        public void RegisterInformer(Informer informer) {
            var position = informer.transform.position;
			NodesArray [(int)position.x / 3, (int)position.z / 3] = new Node (informer,NodeState.Undiscovered);
        }
        
        public void InitializeDebugInfo() {
            if (DebugManagerAStar == null) {
                DebugManagerAStar = GetComponent<DebugManager>();
            }
        }

        [UsedImplicitly]
        public List<Node> AStar(Informer from, Informer to) {
            DebugInformationAlgorithm debugInformation;
            var finalPath = AStar(from, to, false, out debugInformation);
            return finalPath;
        }

        public List<Node> AStar(Informer from, Informer to, bool debugFlag,
            out DebugInformationAlgorithm debugInformation)
        {
            if (from == null || to == null)
            {
                Debug.LogError("Can't run A*. Enter proper from and to parameters!");
                debugInformation = null;
                return null;
            }
            
            if (debugFlag)
            {
                debugInformation = new DebugInformationAlgorithm
                {
                    From = from,
                    To = to,
                    Observed = new List<Node>(),
                    FinalPath = new List<Node>(),
                    LinesFromFinish = new List<Node>(),
                    CrossPoints = new List<Node>()
                };
            }
            else
            {
                debugInformation = null;
            }

            var finish = NodesArray[(int)to.transform.position.x / 3, (int)to.transform.position.z / 3];

            var start = new Tree_Node(null, NodesArray[(int)from.transform.position.x / 3, (int)from.transform.position.z / 3]);
            start.Currentnode.DistanceToFinish = Extensions.Metrics(start, finish);
            start.Currentnode.DistanceToStart = 0;
            start.DistanceFromParent = 0;
            
            var observed = new List<Tree_Node> {start};
            Tree_Node node;
            do
            {
                observed = observed.OrderBy(arg => arg.Currentnode.Visited)
                    .ThenBy(arg => arg.Currentnode.DistanceToStart + arg.Currentnode.DistanceToFinish).ToList();

                node = observed.First();
                observed.First().Currentnode.Visited = NodeState.Processed;
                
                var neighbours = Extensions.NeighboursAStar(node, NodesArray);

                foreach (var neighbour in neighbours)
                {
                    var indexOfExisting = -1;
                    if (observed.Exists(arg => arg.Currentnode.Position == neighbour.Currentnode.Position))
                    {
                        indexOfExisting = observed.FindIndex(arg => arg.Currentnode.Position == neighbour.Currentnode.Position);
                        if (observed[indexOfExisting].Currentnode.Visited != NodeState.Processed)
                            neighbour.Currentnode.DistanceToStart =
                                observed[indexOfExisting].Currentnode.DistanceToStart;
                        else continue;
                    }

                    if (neighbour.Currentnode.DestinationFromPrevious == Destinations.Left
                        || neighbour.Currentnode.DestinationFromPrevious == Destinations.Right
                        || neighbour.Currentnode.DestinationFromPrevious == Destinations.Up
                        || neighbour.Currentnode.DestinationFromPrevious == Destinations.Down)
                        neighbour.DistanceFromParent = distanceStraight;
                    else neighbour.DistanceFromParent = distanceDiagonal;

                    if (neighbour.Currentnode.DistanceToStart < distanceStraight ||
                        node.Currentnode.DistanceToStart + neighbour.DistanceFromParent <
                        neighbour.Currentnode.DistanceToStart)
                    {
                        neighbour.Currentnode.DistanceToStart =
                            node.Currentnode.DistanceToStart + neighbour.DistanceFromParent;
                        neighbour.Parent = node.Currentnode;

                        if (!observed.Exists(arg => arg.Currentnode.Position == neighbour.Currentnode.Position))
                            observed.Add(neighbour);
                        else
                        {
                            observed[indexOfExisting] = neighbour;
                        }
                    }
                }

                if (node.Currentnode.Position == finish.Position) break;

            } while (observed.Exists(arg => arg.Currentnode.Visited != NodeState.Processed));

            //Build shortest path
            var finalPath = new List<Node>();
            if (node.Currentnode.Position == finish.Position)
            {
                while (node.Currentnode.Position != start.Currentnode.Position)
                {
                    finalPath.Add(node.Currentnode);

                    node = observed.Find(arg=>arg.Currentnode.Position == node.Parent.Position);
                }
                finalPath.Reverse();

                if (debugFlag)
                {
                    debugInformation.Observed = Extensions.ToNodes(
                        observed.Where(arg => arg.Currentnode.Visited == NodeState.Processed).
                            OrderBy(arg => arg.Level).ToList());
                    debugInformation.FinalPath = finalPath;
                    Debug.Log("Processed " + debugInformation.Observed.Count);
                }
            }

            return finalPath;
        }
        
        public void CreateVisibilityGraph()
        {
            for (var i = 0; i < JumpPoints.Count - 1; ++i)
            {
                JumpPoints[i].VisibleJP.Add(JumpPoints[i]);
                for (var j = i + 1; j < JumpPoints.Count; ++j)
                {
                    var line = Extensions.BresenhamLineAlgorithm(JumpPoints[i], JumpPoints[j]);
                    if (Extensions.Reachable(line, NodesArray, -1, false))
                    {
                        JumpPoints[i].VisibleJP.Add(JumpPoints[j]);
                        JumpPoints[j].VisibleJP.Add(JumpPoints[i]);
                    }
                }
            }
        }

        public void CreateBounds()
        {
            Boxes.Clear(); 

            var currentBB = 0;
            while(JumpPoints.Exists(arg => arg.BoundingBox == -1))
            {
                JumpPoints = JumpPoints.OrderBy(arg => arg.BoundingBox).ThenBy(arg => arg.Position.x).ToList();

                var currJp = JumpPoints.First();
                currJp.BoundingBox = currentBB;
                
                var currentBbObject = new BoundingBoxes(currJp, currentBB);
                currentBbObject.BoundJP.Add(currJp);

                JumpPoints.Find(arg => arg.Position == currJp.Position).BoundingBox = currentBB;
                var tempJpList = JumpPoints.FindAll(arg => arg.BoundingBox == -1);

                var mean = 0f;

                foreach (var jp in tempJpList)
                {
                    var line = Extensions.BresenhamLineAlgorithm(currJp, jp);

                    if (Extensions.Reachable(line, NodesArray, currentBB, true))
                    {
                        mean += currJp.InformerNode.MetricsAStar(jp.InformerNode);
                        currentBbObject.BoundJP.Add(jp);
                    }
                }

                //Filter points in bound
                mean /= currentBbObject.BoundJP.Count - 1;
                currentBbObject.BoundJP.RemoveAll(arg =>
                    currJp.InformerNode.MetricsAStar(arg.InformerNode) > mean);
                currentBbObject.FilterPointsInBound(NodesArray);
                currentBbObject.FindConvexHull();
                //Find rectangle, that contains current bound
                int left = mapWidth, right = 0, top = mapHeight, bottom = 0;
                foreach (var point in currentBbObject.ConvexHull)
                {
                    if (point.X() < left) left = point.X();
                    if (point.X() > right) right = point.X();
                    if (point.Y() < top) top = point.Y();
                    if (point.Y() > bottom) bottom = point.Y();
                }

                //Mark all points inside convex hull of the bound
                for(var i = left; i<=right; ++i)
                    for(var j = top; j<=bottom; ++j)
                        if (currentBbObject.IsInsideBb(NodesArray[i, j]) && NodesArray[i, j].BoundingBox == -1)
                        {
                            NodesArray[i, j].BoundingBox = currentBB;
                            currentBbObject.BoundNodes.Add(NodesArray[i, j]);
                        }
                
                //Mark Jump Points, that belong to new bound
                for (var i = currentBbObject.BoundJP.Count - 1; i >= 0; --i)
                {
                    JumpPoints.Find(arg => arg.Position == currentBbObject.BoundJP[i].Position).BoundingBox = currentBB;
                }

                Boxes.Add(currentBbObject);
                ++currentBB;
            }

            //Combine bounds
            for (var i = Boxes.Count - 1; i >= 0; --i)
            {
                if (Boxes[i].BoundJP.Count > 3) continue;

                var nothingToBeDone = false;
                foreach (var jp in Boxes[i].BoundJP)
                {
                    var closestJp = BoundingBoxes.FindClosestBound(JumpPoints, jp, NodesArray, true);

                    if (closestJp == null)
                    {
                        nothingToBeDone = true;
                        //Debug.Log("Bound " + Boxes[i].BoxID + " stays even though it shouldn't");

                        break;
                    }
                }

                if(nothingToBeDone) continue;

                for (var j = Boxes[i].BoundJP.Count - 1; j >= 0; --j)
                {
                    var closestJp = BoundingBoxes.FindClosestBound(JumpPoints, Boxes[i].BoundJP[j], NodesArray, true);
                    
                    Boxes[i].BoundJP[j].BoundingBox = closestJp.BoundingBox;
                    Boxes.Find(arg => arg.BoxID == closestJp.BoundingBox).BoundJP.Add(Boxes[i].BoundJP[j]);
                    NodesArray[closestJp.X(), closestJp.Y()].BoundingBox = closestJp.BoundingBox;
                    JumpPoints.Find(arg => arg.Position == closestJp.Position).BoundingBox = closestJp.BoundingBox;
                    Boxes[i].BoundJP.RemoveAt(j);
                }

                Boxes.RemoveAt(i);
            }
        }

        public void PrecomputeJP(int left, int right, int top, int bottom)
		{
            JumpPoints.Clear();
		    JumpPoints = Extensions.FindPrimaryJPWithObstacles(NodesArray, right, bottom);
            
            //computing distances to jump points and obstacles
            for (var i = left; i < right; ++i) {
				for (var j = top; j < bottom; ++j) {
                    if (NodesArray[i, j].InformerNode.IsObstacle) continue;
				    //Checking up
				    var k = 1;
				    while (j + k < bottom)
				    {
                        if (NodesArray[i, j + k].IsJumpPoint == JPType.Primary || NodesArray[i, j + k].InformerNode.IsObstacle)
				        {
                            if (NodesArray[i, j + k].IsJumpPoint == JPType.Primary)
				            {
				                NodesArray[i, j].NormMatrix[0, 1] = k;
                                if (NodesArray[i, j].IsJumpPoint != JPType.Primary)
                                    NodesArray[i,j].IsJumpPoint = JPType.Diagonal;
				            }
				            else
				            {
				                NodesArray[i, j].NormMatrix[0, 1] = -(k - 1);
				            }
				            break;
				        }
				        if (j + k == bottom - 1)
				        {
				            NodesArray[i, j].NormMatrix[0, 1] = -k;
				        }
				        k++;
				    }
				    //Checking down
				    k = 1;
				    while (j - k >= top)
				    {
                        if (NodesArray[i, j - k].IsJumpPoint == JPType.Primary || NodesArray[i, j - k].InformerNode.IsObstacle)
				        {
                            if (NodesArray[i, j - k].IsJumpPoint == JPType.Primary)
				            {
				                NodesArray[i, j].NormMatrix[2, 1] = k;
                                if (NodesArray[i, j].IsJumpPoint != JPType.Primary)
                                    NodesArray[i, j].IsJumpPoint = JPType.Diagonal;
                            }
				            else
				            {
				                NodesArray[i, j].NormMatrix[2, 1] = -(k - 1);
				            }
				            break;
				        }
				        if (j - k == top)
				        {
				            NodesArray[i, j].NormMatrix[2, 1] = -k;
				        }
				        k++;
				    }
				    //Checking right
				    k = 1;
				    while (i + k < right)
				    {
                        if (NodesArray[i + k, j].IsJumpPoint == JPType.Primary || NodesArray[i + k, j].InformerNode.IsObstacle)
				        {
                            if (NodesArray[i + k, j].IsJumpPoint == JPType.Primary)
				            {
				                NodesArray[i, j].NormMatrix[1, 2] = k;
                                if (NodesArray[i, j].IsJumpPoint != JPType.Primary)
                                    NodesArray[i, j].IsJumpPoint = JPType.Diagonal;
                            }
				            else
				            {
				                NodesArray[i, j].NormMatrix[1, 2] = -(k - 1);
				            }
				            break;
				        }
				        if (i + k == right - 1)
				        {
				            NodesArray[i, j].NormMatrix[1, 2] = -k;
				        }
				        k++;
				    }
				    //Checking left
				    k = 1;
				    while (i - k >= left)
				    {
                        if (NodesArray[i - k, j].IsJumpPoint == JPType.Primary || NodesArray[i - k, j].InformerNode.IsObstacle)
				        {
                            if (NodesArray[i - k, j].IsJumpPoint == JPType.Primary)
				            {
				                NodesArray[i, j].NormMatrix[1, 0] = k;
                                if (NodesArray[i, j].IsJumpPoint != JPType.Primary)
                                    NodesArray[i, j].IsJumpPoint = JPType.Diagonal;
                            }
				            else
				            {
				                NodesArray[i, j].NormMatrix[1, 0] = -(k - 1);
				            }
				            break;
				        }
				        if (i - k == left)
				        {
				            NodesArray[i, j].NormMatrix[1, 0] = -k;
				        }
				        k++;
                    }
				}
			}

            //Finding diagonal JP
		    for (var i = left; i < right; ++i)
		    {
		        for (var j = top; j < bottom; ++j)
		        {
                    if (NodesArray[i, j].InformerNode.IsObstacle) continue;
		            //Checking up-right
		            var k = 1;
		            if (!NodesArray[i + 1, j].InformerNode.IsObstacle && !NodesArray[i, j + 1].InformerNode.IsObstacle)
		            {
		                while (i + k < right && j + k < bottom)
		                {
		                    if (NodesArray[i + k, j + k].IsJumpPoint == JPType.Primary ||
		                        NodesArray[i + k, j + k].InformerNode.IsObstacle
		                        || NodesArray[i + k, j + k].NormMatrix[0, 1] > 0
		                        || NodesArray[i + k, j + k].NormMatrix[1, 2] > 0
                                || NodesArray[i + k+1, j + k].InformerNode.IsObstacle
                                || NodesArray[i + k, j + k+1].InformerNode.IsObstacle)
		                    {
		                        if (NodesArray[i + k, j + k].IsJumpPoint == JPType.Primary
		                            || NodesArray[i + k, j + k].NormMatrix[0, 1] > 0
		                            || NodesArray[i + k, j + k].NormMatrix[1, 2] > 0)
		                        {
		                            NodesArray[i, j].NormMatrix[0, 2] = k;
		                        }
                                else if (NodesArray[i + k + 1, j + k].InformerNode.IsObstacle
                                        || NodesArray[i + k, j + k + 1].InformerNode.IsObstacle)
		                        {
                                    NodesArray[i, j].NormMatrix[0, 2] = -k;
                                }
		                        else
		                        {
		                            NodesArray[i, j].NormMatrix[0, 2] = -(k - 1);
		                        }
		                        break;
		                    }
		                    if (i + k == right - 1 || j + k == bottom - 1 )
		                    {
		                        NodesArray[i, j].NormMatrix[0, 2] = -k;
		                    }
		                    k++;
		                }
		            }
		            //Checking down-right
		            k = 1;
                    if (!NodesArray[i + 1, j].InformerNode.IsObstacle && !NodesArray[i, j - 1].InformerNode.IsObstacle)
                    {
                        while (i + k < right && j - k >= top)
                        {
                            if (NodesArray[i + k, j - k].IsJumpPoint == JPType.Primary ||
                                NodesArray[i + k, j - k].InformerNode.IsObstacle
                                || NodesArray[i + k, j - k].NormMatrix[1, 2] > 0
                                || NodesArray[i + k, j - k].NormMatrix[2, 1] > 0
                                || NodesArray[i + k, j - k-1].InformerNode.IsObstacle
                                || NodesArray[i + k+1, j - k].InformerNode.IsObstacle)
                            {
                                if(NodesArray[i + k, j - k].IsJumpPoint == JPType.Primary
                                || NodesArray[i + k, j - k].NormMatrix[1, 2] > 0
                                || NodesArray[i + k, j - k].NormMatrix[2, 1] > 0)
                                {
                                    NodesArray[i, j].NormMatrix[2, 2] = k;
                                }
                                else if (NodesArray[i + k, j - k - 1].InformerNode.IsObstacle
                                         || NodesArray[i + k + 1, j - k].InformerNode.IsObstacle)
                                {
                                    NodesArray[i, j].NormMatrix[2, 2] = -k;
                                }
                                else
                                {
                                    NodesArray[i, j].NormMatrix[2, 2] = -(k - 1);
                                }
                                break;
                            }
                            if (i + k == bottom - 1 || j - k == top)
                            {
                                NodesArray[i, j].NormMatrix[2, 2] = -k;
                            }
                            k++;
                        }
                    }
		            //Checking up-left
		            k = 1;
                    if (!NodesArray[i - 1, j].InformerNode.IsObstacle && !NodesArray[i, j + 1].InformerNode.IsObstacle)
                    {
                        while (i - k >= left && j + k < bottom)
                        {
                            if (NodesArray[i - k, j + k].IsJumpPoint == JPType.Primary ||
                                NodesArray[i - k, j + k].InformerNode.IsObstacle
                                || NodesArray[i - k, j + k].NormMatrix[0, 1] > 0
                                || NodesArray[i - k, j + k].NormMatrix[1, 0] > 0
                                || NodesArray[i - k-1, j + k].InformerNode.IsObstacle
                                || NodesArray[i - k, j + k+1].InformerNode.IsObstacle)
                            {
                                if (NodesArray[i - k, j + k].IsJumpPoint == JPType.Primary
                                    || NodesArray[i - k, j + k].NormMatrix[0, 1] > 0
                                    || NodesArray[i - k, j + k].NormMatrix[1, 0] > 0)
                                {
                                    NodesArray[i, j].NormMatrix[0, 0] = k;
                                }
                                else if (NodesArray[i - k - 1, j + k].InformerNode.IsObstacle
                                        || NodesArray[i - k, j + k + 1].InformerNode.IsObstacle)
                                {
                                    NodesArray[i, j].NormMatrix[0, 0] = -k;
                                }
                                else
                                {
                                    NodesArray[i, j].NormMatrix[0, 0] = -(k - 1);
                                }
                                break;
                            }
                            if (i - k == left || j + k == bottom - 1)
                            {
                                NodesArray[i, j].NormMatrix[0, 0] = -k;
                            }
                            k++;
                        }
                    }
		            //Checking down-left
		            k = 1;
                    if (!NodesArray[i - 1, j].InformerNode.IsObstacle && !NodesArray[i, j - 1].InformerNode.IsObstacle)
                    {
                        while (i - k >= left && j - k >= top)
                        {
                            if (NodesArray[i - k, j - k].IsJumpPoint == JPType.Primary ||
                                NodesArray[i - k, j - k].InformerNode.IsObstacle
                                || NodesArray[i - k, j - k].NormMatrix[1, 0] > 0
                                || NodesArray[i - k, j - k].NormMatrix[2, 1] > 0
                                || NodesArray[i - k-1, j - k].InformerNode.IsObstacle
                                || NodesArray[i - k, j - k-1].InformerNode.IsObstacle)
                            {
                                if (NodesArray[i - k, j - k].IsJumpPoint == JPType.Primary
                                    || NodesArray[i - k, j - k].NormMatrix[1, 0] > 0
                                    || NodesArray[i - k, j - k].NormMatrix[2, 1] > 0)
                                {
                                    NodesArray[i, j].NormMatrix[2, 0] = k;
                                }
                                else if (NodesArray[i - k - 1, j - k].InformerNode.IsObstacle
                                         || NodesArray[i - k, j - k - 1].InformerNode.IsObstacle)
                                {
                                    NodesArray[i, j].NormMatrix[2, 0] = -k;
                                }
                                else
                                {
                                    NodesArray[i, j].NormMatrix[2, 0] = -(k - 1);
                                }
                                break;
                            }
                            if (i - k == left || j - k == top)
                            {
                                NodesArray[i, j].NormMatrix[2, 0] = -k;
                            }
                            k++;
                        }
                    }
                    /*var tileText = NodesArray[i, j].InformerNode.GetComponentInChildren<TextMesh>();
                    string text = "";
                    for (int m = 0; m < 3; ++m)
                    {
                        for (int n = 0; n < 3; ++n)
                            text = text + NodesArray[i, j].NormMatrix[m, n] + " ";
                        text = text + "\n";
                    }

                    tileText.text = text;*/
		        }
		    }
		}

        public void PrecomputeRoutesBetweenBb()
        {
            for (var i = 0; i < Boxes.Count - 1; ++i)
            {
                for (var j = i + 1; j < Boxes.Count; ++j)
                {
                    DebugInformationAlgorithm debugInformation;
                    var path = JPS(Boxes[i].StartJP.InformerNode, Boxes[j].StartJP.InformerNode, false, out debugInformation, false);

                    var boundsList = new List<int>();

                    if (path != null)
                        foreach (var node in path)
                            if (!boundsList.Contains(node.BoundingBox)) boundsList.Add(node.BoundingBox);
                    
                    Boxes[i].RoutesToOtherBB.Add(Boxes[j].BoxID, boundsList);
                    Boxes[j].RoutesToOtherBB.Add(Boxes[i].BoxID, boundsList);
                }
            }
        }

        public void InitializeNodesWithBB(int left, int right, int top, int bottom)
        {
            for (var i = left; i < right; ++i)
            {
                for (var j = top; j < bottom; ++j)
                {
                    if (NodesArray[i, j].BoundingBox != -1) continue;

                    var closestJp =  BoundingBoxes.FindClosestBound(JumpPoints, NodesArray[i, j], NodesArray, false);
                    NodesArray[i, j].BoundingBox = (closestJp != null) ? closestJp.BoundingBox : -1;
                }
            }
        }

        public void PrecomputeMap()
        {
            PrecomputeMap(0, mapHeight, 0, mapWidth);
        }

        public void PrecomputeMap(int left, int right, int top, int bottom)
        {
            //CleanUp
            for (var i = left; i < right; ++i)
                for (var j = top; j < bottom; ++j)
                    NodesArray[i, j].RestartNode();

            PrecomputeJP(left, right, top, bottom);
            
            //Create visibility graph
            CreateVisibilityGraph();

            //Prepare for Goal bounding
            CreateBounds();

            //Initialize map nodes with closest BB
            InitializeNodesWithBB(left, right, top, bottom);

            //Save routes between bounds
            PrecomputeRoutesBetweenBb();

            IsPrecomputed = true;
        }

        public List<Node> JPS(Informer from, Informer to)
        {
            DebugInformationAlgorithm debugInformation;
            var finalPath = JPS(from, to, false, out debugInformation, true);
            return finalPath;

        }

        public List<Node> JPS(Informer from, Informer to, bool debugFlag, out DebugInformationAlgorithm debugInformation, bool useGB)
        { 
            if (from == null || to == null)
            {
                Debug.LogError("Can't run JPS+. Enter proper from and to parameters!");
                debugInformation = null;
                return null;
            }
            
		    var finish = NodesArray[(int)to.transform.position.x/3, (int)to.transform.position.z/3];
            var linesFromFinish = new StraightLinesFromNode(finish);

            var start = new Tree_Node(null,NodesArray[(int)from.transform.position.x/3, (int)from.transform.position.z/3]);
		    start.Currentnode.DistanceToFinish = Extensions.MetricsSqrt(start.Currentnode,finish);
		    var current = start;

            //Find closest bound to finish
            var routeBound = new List<int>();
            if (IsPrecomputed && Boxes.Count > 0)
            {
                Debug.Log("startBound = " + start.Currentnode.BoundingBox);
                Debug.Log("finishBound = " + finish.BoundingBox);

                routeBound = Boxes.Find(arg => arg.BoxID == start.Currentnode.BoundingBox).RoutesToOtherBB[finish.BoundingBox];
            }   
            
		    var path = new List<Tree_Node>();
		    var observed = new List<Tree_Node> {current};

            if (debugFlag)
            {
                debugInformation = new DebugInformationAlgorithm
                {
                    From = from,
                    To = to,
                    Observed = new List<Node>(),
                    FinalPath = new List<Node>(),
                    LinesFromFinish = new List<Node>(),
                    CrossPoints = new List<Node>()
                };
            }
            else
            {
                debugInformation = null;
            }

            if ((finish.BoundingBox == -1 || start.Currentnode.BoundingBox == -1) && Boxes.Count > 0 && useGB)
            {
                Debug.Log("No path exists");

                return null;
            }

            while (current.Currentnode != finish)
		    {
                if (!observed.Exists(arg => arg.Currentnode.Visited != NodeState.Processed))
		        {
		            /*if (IsPrecomputed)
		            {
		                Debug.Log("No path was found between bb "+start.Currentnode.BoundingBox+" "+finish.BoundingBox);
		            }*/
                    if (debugFlag)
                    {
                        debugInformation.Observed = Extensions.ToNodes(
                            observed.Where(arg => arg.Currentnode.Visited == NodeState.Processed).
                            OrderBy(arg => arg.Level).ToList());
                    }
                    return null;
		        }
                observed[0].Currentnode.Visited = NodeState.Processed;


                //Go to finish if in Target JP
                current.Currentnode = Extensions.IsTargetJP(current.Currentnode, linesFromFinish);
                if (current.Currentnode.TargetJP && Extensions.Reachable(current.Currentnode, finish, NodesArray))
		        {
		            finish.DestinationFromPrevious = Extensions.DestinationInverse(current.Currentnode.DestinationToFinish);
		            path.Add(current);
		            current = new Tree_Node(current, finish);
		            path.Add(current);
		            break;
		        }

                //Find next nodes

                //Neighbours
                var neighbours = Extensions.Neighbours(current, NodesArray, finish);

                //Target JP
                var lines = new StraightLinesFromNode(current.Currentnode,Extensions.GetDestinationsFromNeighbours(neighbours));

                var minMetrics = 1000000f;
                var tempList = new List<Tree_Node>();
                if (lines.Lines != null)
                {
                    foreach (var lineFromFinish in linesFromFinish.Lines)
                    {
                        foreach (var line in lines.Lines)
                        {
                            var coordinates = StraightLine.Crossing(line, lineFromFinish);

                            if (coordinates != null && !NodesArray[coordinates.X, coordinates.Y].InformerNode.IsObstacle
                                && Extensions.Reachable(NodesArray[coordinates.X, coordinates.Y], finish, NodesArray)
                                && Extensions.Reachable(current.Currentnode, NodesArray[coordinates.X, coordinates.Y], NodesArray))
                            {
                                var tempNode = new Tree_Node(current, NodesArray[coordinates.X, coordinates.Y]);
                                tempNode.Currentnode.DistanceToFinish = Extensions.Metrics(tempNode, finish);
                                tempNode.Currentnode.TargetJP = true;
                                tempNode.Currentnode.DestinationToFinish = Extensions.DestinationInverse(lineFromFinish.Destination);
                                tempNode.Currentnode.Visited = NodeState.Discovered;
                                if (tempNode.Currentnode.DistanceToFinish < minMetrics)
                                {
                                    minMetrics = tempNode.Currentnode.DistanceToFinish;
                                    tempList.Clear();
                                    tempList.Add(tempNode);
                                }
                                else if (Math.Abs(tempNode.Currentnode.DistanceToFinish - minMetrics) < 0.00000000001)
                                {
                                    tempList.Add(tempNode);
                                }
                            }
                        }
                    }
                }

                Tree_Node tempTargetJP = null;
                if (tempList.Count != 0)
                {
                    tempTargetJP = tempList[0];
                    if (tempList.Count > 1)
                    {
                        var min = tempTargetJP.DistanceFromParent;
                        foreach (var node in tempList)
                        {
                            if (node.DistanceFromParent < min)
                            {
                                tempTargetJP = node;
                                min = node.DistanceFromParent;
                            }
                        }
                    }

                    tempTargetJP.Currentnode.DistanceToFinish = Extensions.MetricsSqrt(tempTargetJP.Currentnode, finish);
                    tempTargetJP.DistanceFromParent = Extensions.MetricsSqrt(tempTargetJP.Currentnode, current.Currentnode);
                    tempTargetJP.Currentnode.DistanceToStart =
                        current.Currentnode.DistanceToStart + tempTargetJP.DistanceFromParent;

                    if (!observed.Exists(arg => arg.Currentnode.Position == tempTargetJP.Currentnode.Position))
                        observed.Add(tempTargetJP);
                    else
                    {
                        var index =
                            observed.FindIndex(arg => arg.Currentnode.Position == tempTargetJP.Currentnode.Position);

                        if (observed[index].Currentnode.Visited == NodeState.Discovered)
                            observed[index] = tempTargetJP;
                    }
                }

                /*//Debug
		        if (IsPrecomputed)
		            Debug.Log("current = " + current.Currentnode.Position +
		                      " neighbours = " + neighbours.Count +
		                      " DistanceToStart = " + current.Currentnode.DistanceToStart +
		                      " DistanceToFinish = " + current.Currentnode.DistanceToFinish +
		                      " Level =  " + current.Level +
		                      " BB = " + current.Currentnode.BoundingBox);*/

                if (neighbours.Count != 0)
                {
                    foreach(var neighbour in neighbours)
                    {
                        //Use Goal bounding to eliminate neighbours
                        if (useGB && !routeBound.Exists(arg => arg == neighbour.Currentnode.BoundingBox))
                        {
                            /*//Debug
                            Debug.Log("Eliminate neighbour Primary JP Bound "+neighbour.Currentnode.BoundingBox);*/

                            continue;
                        }

                        var indexOfExisting = -1;
                        if (observed.Exists(arg => arg.Currentnode.Position == neighbour.Currentnode.Position))
                        {
                            indexOfExisting = observed.FindIndex(arg => arg.Currentnode.Position == neighbour.Currentnode.Position);
                            if (observed[indexOfExisting].Currentnode.Visited != NodeState.Processed)
                                neighbour.Currentnode.DistanceToStart =
                                    observed[indexOfExisting].Currentnode.DistanceToStart;
                            else continue;
                        }
                        
                        if (neighbour.Currentnode.DistanceToStart < distanceStraight ||
                            current.Currentnode.DistanceToStart + neighbour.DistanceFromParent <
                            neighbour.Currentnode.DistanceToStart)
                        {
                           neighbour.Currentnode.DistanceToStart =
                                current.Currentnode.DistanceToStart + neighbour.DistanceFromParent;
                            neighbour.Currentnode.DistanceToFinish = Extensions.MetricsSqrt(neighbour.Currentnode, finish);
                            neighbour.Parent = current.Currentnode;

                            if (indexOfExisting == -1 && Extensions.SelectJPFromNeighbours(current, neighbour))
                                observed.Add(neighbour);
                            else if(indexOfExisting != -1)
                            {
                                observed[indexOfExisting] = neighbour;
                            }
                        }
                    }
                }

		        observed = observed.OrderBy(arg => arg.Currentnode.Visited)
		            .ThenBy(arg => arg.Currentnode.DistanceToStart + arg.Currentnode.DistanceToFinish).ToList();
                path.Add(current);

		        current = observed[0];

		    }

            //Debug.Log("Path: "+path.Count);
            if(path.Count>1)
            {
                var finalPath = new List<Node>();
                while (current!=start)
                {
                    var middlePoints = StraightLine.FindMiddlePoints(current.Parent, current.Currentnode);
                    if(current.Parent!=start.Currentnode) middlePoints.RemoveAt(0);
                    finalPath.InsertRange(0,Extensions.ToNodes(middlePoints, NodesArray));
                    current = path.Find(arg => arg.Currentnode.Position == current.Parent.Position &&
                    arg.Level == current.Level-1);
                }

                if (debugFlag)
                {
                    debugInformation.Observed = Extensions.ToNodes(
                        observed.Where(arg => arg.Currentnode.Visited==NodeState.Processed).
                        OrderBy(arg => arg.Level).ToList());
                    debugInformation.FinalPath = finalPath;
                    Debug.Log("Processed " + debugInformation.Observed.Count);
                }
                //Debug.Log("Final path: " + finalPath.Count);
                return finalPath;
		    }
            else return null;
		}
    }
}