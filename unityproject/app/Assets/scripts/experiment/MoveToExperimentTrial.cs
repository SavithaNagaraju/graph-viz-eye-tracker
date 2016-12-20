﻿using UnityEngine;
using System.Collections;

public class MoveToExperimentTrial : ExperimentTrial
{
    private Graph _graph;
    bool first = true;
    bool currentlyHighlighted = false;
    int hightlightCounter = 0;
    bool graphCreated = false;
    public static Color colorDesAuserwaehlten = new Color(1.0f, 0.0f, 0.0f);
    Node selectedNode;
    public bool done = false;
    
    public Graph Graph
    {
        get
        {
            return _graph;
        }
    }
    public void initialze()
    {
        if (first)
        {

            Bubble.changeBubbleSize(_graph.BubbleSize);
            first = false;
            createGraph();
            experimentLogger.getLogger().currentGraph = _graph.Name;
            experimentLogger.getLogger().bubbleSize = _graph.BubbleSize.ToString();
            graphCreated = true;

        }
    }

    public void update()
    {
        if(!currentlyHighlighted && graphCreated)
        {
            if(hightlightCounter < _graph.NumberHighlightedNodes) { 
                currentlyHighlighted = true;
                highlight();
            }
        }
        else if(currentlyHighlighted && selectedNode.gotHit)
        {
                selectedNode.derAuserwaehlte = false;
                Bubble.moveTo(Bubble.REST_POS);
                selectedNode = null;
                hightlightCounter++;

            if (hightlightCounter < _graph.NumberHighlightedNodes)
            {
                highlight();
            }

            else
            {
                done = true;
            }
            
        }
        if(selectedNode != null)
        {
            selectedNode.Highlight(colorDesAuserwaehlten);
        }
        
    }
    private void highlight()
    {
        Node node = Node.GetNodeWithId(Random.Range(0, Graph.NumNodes));
        node.Highlight(colorDesAuserwaehlten);
        selectedNode = node;
        node.derAuserwaehlte = true;
        experimentLogger.getLogger().currentHighlightedNode = node.id_string;
    }
    private void createGraph()
    {
        GameController gc = GameObject.Find("GameController").GetComponent<GameController>();
        gc.createGraph(_graph.Name);
    }
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="_targetPosition"></param>
    public MoveToExperimentTrial(int trialnum, Graph graph) : base(trialnum)
    {
        _graph = graph;
    }
}
