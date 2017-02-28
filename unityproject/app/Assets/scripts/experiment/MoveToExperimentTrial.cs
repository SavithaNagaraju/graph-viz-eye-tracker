﻿using UnityEngine;
using System.Collections;

public class MoveToExperimentTrial : ExperimentTrial
{

	private Graph _graph;
	bool first = true;
	int hightlightCounter = 0;
	bool graphCreated = false;
	public static Color colorDesAuserwaehlten = new Color (1.0f, 0.0f, 0.0f);
	public static Color colorDesGefundenenAuserwaehlten = new Color (1.0f, 1.0f, 0.0f);
	Node nodeToFind;
	public bool done = false;


	public Graph Graph {
		get {
			return _graph;
		}
	}

	public void initialze (GameObject gameController)
	{
		
		if (first) {
			if (Graph.ExperimentType == experimentType.EYE) {
				gameController.GetComponent<Bubble> ().rayCastAllowed = true;
			} else if (Graph.ExperimentType == experimentType.MOUSE) {
				gameController.GetComponent<Bubble> ().rayCastAllowed = false;
			}
			Bubble.changeBubbleSize (_graph.BubbleSize);

			createGraph ();
			highlight ();
			graphCreated = true;

			Vector3 pos = Node.GetNodeWithId(0).transform.position;
            GameObject.FindGameObjectWithTag("metaCamera").transform.position = new Vector3(0, 0, -40);
            Camera.main.transform.localEulerAngles = Vector3.zero;
            Camera.main.transform.position = new Vector3(0, 0, -40);
            GameObject cr = GameObject.FindGameObjectWithTag("RightCam");
            if(cr) {
                cr.transform.position = new Vector3(0, 0, -40);
                cr.transform.localEulerAngles = Vector3.zero;
            }


            experimentLogger.getLogger ().currentGraphSize = _graph.NumNodes;
            experimentLogger.getLogger().condition = _graph.ExperimentType.ToString();
            experimentLogger.getLogger ().bubbleSize = _graph.BubbleSize.ToString ();
			first = false;
		}
       
	}

	private void resetColor ()
	{

		try {
			if (nodeToFind.GetComponent<Node> ().id != GameObject.FindGameObjectWithTag ("GameController").GetComponent<HapringController> ().currentIndex) {
				nodeToFind.Highlight (colorDesAuserwaehlten);
			} else {
				nodeToFind.Highlight (colorDesGefundenenAuserwaehlten);
			}
		} catch (System.Exception ex) {
			
		}
	}

	public void update ()
	{
		if (!first) {
			if (hightlightCounter < _graph.NumberHighlightedNodes && !done) {
				if (nodeToFind.gotHit) {
					Bubble.moveTo (Bubble.REST_POS);
					hightlightCounter++;
					Debug.LogWarning ("Got Hit. Current hits: " + hightlightCounter + " Needed: " + _graph.NumberHighlightedNodes);
					nodeToFind.derAuserwaehlte = false;
					nodeToFind.gotHit = false;
					nodeToFind.HighlightDefault ();
					highlight ();
			
				}
				resetColor ();
			} else {
				done = true;
			}
		}

			
		      
	}

	private void highlight ()
	{
		Node node = Node.GetNodeWithId (Random.Range (0, Graph.NumNodes));
		node.Highlight (colorDesAuserwaehlten);
		node.derAuserwaehlte = true;
		nodeToFind = node;
		experimentLogger.getLogger ().targetNode = node.id_string;
	}

	private void createGraph ()
	{
		
		GameController gc = GameObject.Find ("GameController").GetComponent<GameController> ();
		gc.createGraph (_graph.Name);
	}

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="_targetPosition"></param>
	public MoveToExperimentTrial (int trialnum, Graph graph) : base (trialnum)
	{
		_graph = graph;
	}
}
