﻿using UnityEngine;
using System.Collections.Generic;
using Assets.Scripts.Core;
using Assets.Scripts.Map;
using Assets.Scripts.PathFinding;
using Random = System.Random;

public class OnA_StarGUI : MonoBehaviour {
    public List<Texture2D> Maps;
    private int _currentMap ;
    public Informer StartInformer;
    public Informer FinishInformer;
    public GameObject Map;
    private Renderer _startRenderer;
    private Color _startColor;
    private Renderer _finishRenderer;
    private Color _finishColor;
    private bool _startChanged;
    private bool _selectNodeForBB;
    private bool _changeMapDown;
    private bool _useGB;
    private int _changedRectLeft;
    private int _changedRectRight;
    private int _changedRectTop;
    private int _changedRectBottom;

    public void RunJPS()
    {
        //Navigation
        var controller = GetComponentInChildren<Controller>();

        if(!controller.IsPrecomputed) PrecomputeMap();

        DebugInformationAlgorithm debugInformation;
        controller.JPS(StartInformer, FinishInformer, true, out debugInformation, _useGB);
        controller.InitializeDebugInfo();
        controller.DebugManagerAStar.AddPath(debugInformation);

        //Coloring back
        _startRenderer.material.SetColor("_Color", _startColor);
        _finishRenderer.material.SetColor("_Color", _finishColor);
        _startChanged = false;
    }

    public void RunAStar()
    {
        //Navigation
        var controller = GetComponentInChildren<Controller>();

        DebugInformationAlgorithm debugInformation;
        controller.AStar(StartInformer, FinishInformer, true, out debugInformation);
        controller.InitializeDebugInfo();
        controller.DebugManagerAStar.AddPath(debugInformation);

        //Coloring back
        _startRenderer.material.SetColor("_Color", _startColor);
        _finishRenderer.material.SetColor("_Color", _finishColor);
        _startChanged = false;
    }

    public void NextMap()
    {
        var mapManager = GetComponentInChildren<MapManager>();
        var controller = mapManager.GetComponent<Controller>();
        if (_currentMap == Maps.Count - 1) _currentMap = 0;
        else ++_currentMap;
        mapManager.Map = Maps[_currentMap];
        var area = mapManager.GetComponentsInChildren<Informer>();
        foreach (var informer in area)
        {
            Destroy(informer.gameObject);
        }
        for (var i = 0; i < mapManager.Map.height; ++i)
        {
            for (var j = 0; j < mapManager.Map.width; ++j)
            {
                var color = mapManager.Map.GetPixel(i, j);
                var prefab = mapManager.TilesM.GetPrefab(color);
                if (prefab == null)
                {
                    continue;
                }

                var position = new Vector3(i * 3.0f, 0.0f, j * 3.0f);
                var temp = Instantiate(prefab, position, Quaternion.identity) as GameObject;
                if (temp != null)
                {
                    temp.transform.parent = mapManager.gameObject.transform;
                }
            }
        }
        controller.IsPrecomputed = false;
    }

    public void ChooseNodesRandomly()
    {
        var controller = GetComponentInChildren<Controller>();
        var mapManager = GetComponentInChildren<MapManager>();

        var rnd = new Random();
        var index1 = Vector2.zero;
        var index2 = Vector2.zero;
        while (index1 ==  index2 || index1 == Vector2.zero || index2 == Vector2.zero)
        {
            var x = rnd.Next(0, mapManager.Map.height - 1);
            var y = rnd.Next(0, mapManager.Map.width - 1);

            if (index1 == Vector2.zero)
            {
                if (!controller.NodesArray[x, y].InformerNode.IsObstacle) index1 = new Vector2(x, y);
                continue;
            }

            if (index2 == Vector2.zero)
            {
                if (!controller.NodesArray[x, y].InformerNode.IsObstacle) index2 = new Vector2(x, y);
            }
        }

        ColorStartAndFinish(controller.NodesArray[(int)index1.x, (int)index1.y].InformerNode,
            controller.NodesArray[(int)index2.x, (int)index2.y].InformerNode);

    }

    public void JPSTest()
    {
        var controller = GetComponentInChildren<Controller>();
        
        if(!controller.IsPrecomputed) PrecomputeMap();

        ChooseNodesRandomly();

        DebugInformationAlgorithm debugInformation;
        controller.JPS(StartInformer, FinishInformer, true, out debugInformation, true);
        controller.InitializeDebugInfo();
        controller.DebugManagerAStar.AddPath(debugInformation);

        //Coloring back
        _startRenderer.material.SetColor("_Color", _startColor);
        _finishRenderer.material.SetColor("_Color", _finishColor);
    }

    public void AStarTest()
    {
        var controller = GetComponentInChildren<Controller>();

        ChooseNodesRandomly();

        DebugInformationAlgorithm debugInformation;
        controller.AStar(StartInformer, FinishInformer, true, out debugInformation);
        controller.InitializeDebugInfo();
        controller.DebugManagerAStar.AddPath(debugInformation);

        //Coloring back
        _startRenderer.material.SetColor("_Color", _startColor);
        _finishRenderer.material.SetColor("_Color", _finishColor);
    }

    private void ColorStartAndFinish(Informer start, Informer finish)
    {
        if (_startRenderer != null)
        {
            _startRenderer.material.SetColor("_Color", _startColor);
        }

        StartInformer = start;
        _startRenderer = StartInformer.GetComponent<Renderer>();
        _startColor = _startRenderer.material.GetColor("_Color");
        _startRenderer.material.SetColor("_Color", Color.cyan);

        Debug.Log("Start" + StartInformer.transform.position);

        if (_finishRenderer != null)
        {
            _finishRenderer.material.SetColor("_Color", _finishColor);
        }

        FinishInformer = finish;
        _finishRenderer = FinishInformer.GetComponent<Renderer>();
        _finishColor = _finishRenderer.material.GetColor("_Color");
        _finishRenderer.material.SetColor("_Color", Color.magenta);

        Debug.Log("Finish" + FinishInformer.transform.position);
    }

    public void PrecomputeMap()
    {
        var controller = GetComponentInChildren<Controller>();
        if(!controller.IsPrecomputed) controller.PrecomputeMap();
    }

    public void ClearMap()
    {
        var colorList = GetComponent<DefaultColours>();
        if (colorList.DefaultMaterials.Count == 0) return;
        var controller = GetComponentInChildren<Controller>();
        var nodesArray = controller.NodesArray;
        for (var j = 0; j < Maps[_currentMap].height; ++j)
        {
            for (var k = 0; k < Maps[_currentMap].width; ++k)
            {
                if (!nodesArray[j, k].InformerNode.IsObstacle)
                {
                    var currentRenderer = nodesArray[j,k].InformerNode.GetComponent<Renderer>();
                    currentRenderer.material = colorList.DefaultMaterials[0];
                }
            }
        }
        Extensions.ShowJP(controller.JumpPoints);
        if(_startRenderer != null) _startRenderer.material.SetColor("_Color", Color.cyan);
        if(_finishRenderer != null) _finishRenderer.material.SetColor("_Color", Color.magenta);
    }

    public void ShowFinalPathAStar()
    {
        if (StartInformer != null && FinishInformer != null)
        {
            var controller = GetComponentInChildren<Controller>();
            var path = controller.AStar(StartInformer, FinishInformer);
            var debugInfo = new DebugInformationAlgorithm
            {
                From = StartInformer,
                To = FinishInformer,
                FinalPath = path,
                Destroy = false
            };
            controller.InitializeDebugInfo();
            controller.DebugManagerAStar.AddPath(debugInfo);
        }
        else Debug.LogError("Enter proper arguments");
    }

    public void ShowFinalPathJPS()
    {
        if (StartInformer != null && FinishInformer != null)
        {
            var controller = GetComponentInChildren<Controller>();
            var path = controller.JPS(StartInformer, FinishInformer);
            var debugInfo = new DebugInformationAlgorithm
            {
                From = StartInformer,
                To = FinishInformer,
                FinalPathJPS = path,
                Destroy = false
            };
            controller.InitializeDebugInfo();

            if(!controller.IsPrecomputed) PrecomputeMap();

            controller.DebugManagerAStar.AddPath(debugInfo);
        }
        else Debug.LogError("Enter proper arguments");
    }

    public void ShowNeighboursAndTJP()
    {
        var controller = GetComponentInChildren<Controller>();
        if (!controller.IsPrecomputed) controller.PrecomputeMap();
        if (StartInformer != null && FinishInformer != null)
        {
            DebugInformationAlgorithm debugInformation;
            controller.InitializeDebugInfo();
            Extensions.NeighboursAndTJP(StartInformer, FinishInformer, controller.NodesArray, out debugInformation);
            controller.DebugManagerAStar.AddPath(debugInformation);

        }
        else
        {
            Debug.LogError("Enter input arguments");
            Extensions.ShowJP(controller.JumpPoints);
        }
    }

    public void ShowAllBounds()
    {
        var controller = GetComponentInChildren<Controller>();
        if (!controller.IsPrecomputed) controller.PrecomputeMap();

        var mapManager = GetComponentInChildren<MapManager>();

        for (var i = 0; i < mapManager.Map.height; ++i)
        for (var j = 0; j < mapManager.Map.width; ++j)
        {
            var tileText = controller.NodesArray[i, j].InformerNode.GetComponentInChildren<TextMesh>();
            var text = "\n\t" + controller.NodesArray[i, j].BoundingBox;
            tileText.text = text;
        }
    }

    public void ChangeMap()
    {
        var mapManager = GetComponentInChildren<MapManager>();
        var controller = GetComponentInChildren<Controller>();

        controller.IsPrecomputed = false;

        RaycastHit hit;
        var cam = FindObjectOfType<Camera>();
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out hit))
        {
            var tile = hit.collider.gameObject;

            var selectedNode = tile.GetComponent<Informer>();
            var x = (int) selectedNode.transform.position.x / 3;
            var y = (int) selectedNode.transform.position.z / 3;

            if (x < _changedRectLeft) _changedRectLeft = x;
            if (x > _changedRectRight) _changedRectRight = y;
            if (y < _changedRectTop) _changedRectTop = y;
            if (y < _changedRectBottom) _changedRectBottom = y;

            //Changing borders not allowed
            if (x == 0 || y == 0 || x == mapManager.Map.height - 1 || y == mapManager.Map.width - 1)
            {
                Debug.LogWarning("Changing borders of the map not allowed");
                return;
            }

            var informer = controller.NodesArray[x, y].InformerNode;
            informer.IsObstacle = !informer.IsObstacle;

            controller.NodesArray[x, y] = new Node(informer, NodeState.Undiscovered);

            var prefab = mapManager.TilesM.GetPrefab(informer.IsObstacle);
            if (prefab == null)
            {
                Debug.LogError("No such tile!");
                return;
            }

            Destroy(informer.gameObject);

            var position = new Vector3(x * 3.0f, 0.0f, y * 3.0f);
            var temp = Instantiate(prefab, position, Quaternion.identity) as GameObject;
            if (temp != null)
            {
                temp.transform.parent = mapManager.gameObject.transform;
                informer = temp.GetComponent<Informer>();
                controller.RegisterInformer(informer);
            }

        }
    }

    public void ChangeMapWrapper(bool toggleVal)
    {
        _changedRectLeft = 100000;
        _changedRectRight = 0;
        _changedRectTop = 100000;
        _changedRectBottom = 0;

        var controller = GetComponentInChildren<Controller>();

        if(toggleVal)controller.PrecomputeMap(_changedRectLeft, _changedRectRight, _changedRectTop, _changedRectBottom);

        _changeMapDown = !_changeMapDown;
    }

    public void UseGBwithJPS(bool toggleVal)
    {
        _useGB = !_useGB;
    }

    public void ShowBound()
    {
        var controller = GetComponentInChildren<Controller>();

        RaycastHit hit;
        var cam = FindObjectOfType<Camera>();
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out hit))
        {
            var tile = hit.collider.gameObject;

            var selectedNode = tile.GetComponent<Informer>();
            var x = (int) selectedNode.transform.position.x / 3;
            var y = (int) selectedNode.transform.position.z / 3;

            Debug.Log("Selected " + selectedNode.transform.position + " JP type = " +
                      controller.NodesArray[x, y].IsJumpPoint);

            if (controller.NodesArray[x, y].IsJumpPoint != JPType.Primary)
            {
                Debug.LogWarning("You need to select Primary JP");
                return;
            }

            var box = controller.Boxes.Find(arg => arg.BoxID == controller.NodesArray[x, y].BoundingBox);

            foreach (var jp in box.BoundJP)
            {
                var renderer = jp.InformerNode.GetComponent<Renderer>();
                renderer.material.SetColor("_Color", jp != box.StartJP ? Color.yellow : Color.red);

                var tileText = jp.InformerNode.GetComponentInChildren<TextMesh>();
                var text = "\n\t" + box.BoxID;
                tileText.text = text;
            }
        }
    }

    public void ShowBoundWrapper(bool toggleVal)
    {
        PrecomputeMap();

        _selectNodeForBB = !_selectNodeForBB;
    }

    // Use this for initialization
    void Start () {
	    _currentMap = 0;
	    _startChanged = false;
        _selectNodeForBB = false;
        _changeMapDown = false;
        _useGB = false;
    }

    void Update()
    {
        if (Input.GetButtonDown("Fire1") && !_selectNodeForBB && !_changeMapDown)
        {
            RaycastHit hit;
            var cam = FindObjectOfType<Camera>();
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out hit))
            {
                var tile = hit.collider.gameObject;
                if (!_startChanged)
                {
                    _startChanged = true;
                    if (_startRenderer != null)
                    {
                        _startRenderer.material.SetColor("_Color", _startColor);
                    }

                    StartInformer = tile.GetComponent<Informer>();
                    _startRenderer = StartInformer.GetComponent<Renderer>();
                    _startColor = _startRenderer.material.GetColor("_Color");
                    _startRenderer.material.SetColor("_Color", Color.cyan);

                    Debug.Log("Start" + StartInformer.transform.position);
                }
                else
                {
                    _startChanged = false;
                    if (_finishRenderer != null)
                    {
                        _finishRenderer.material.SetColor("_Color", _finishColor);
                    }

                    FinishInformer = tile.GetComponent<Informer>();
                    _finishRenderer = FinishInformer.GetComponent<Renderer>();
                    _finishColor = _finishRenderer.material.GetColor("_Color");
                    _finishRenderer.material.SetColor("_Color", Color.magenta);

                    Debug.Log("Finish" + FinishInformer.transform.position);
                }
            }
        }
        if (Input.GetButtonDown("Fire1") && _changeMapDown && !_selectNodeForBB) ChangeMap();
        if (Input.GetButtonDown("Fire1") && !_changeMapDown && _selectNodeForBB) ShowBound();
    }
}
