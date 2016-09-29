﻿using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using SimpleJSON;

public class InfoLabelController : MonoBehaviour
{

	private GameObject header;
	private GameObject desc;
	public KeyCode dismissKey;



	// Update is called once per frame
	void Update ()
	{
		Node node = MouseClickUtil.checkMouseClick (0, 10000);
		if (node != null) {

			setTitle (node.title);
			if (node.desc == "Loading ...") StartCoroutine(parseDescription (node));
			setDescription (node.desc);
			toggle (true);
		}

		if (Input.GetKeyDown (dismissKey)) {
			toggle (false);
			setTitle ("");
			setDescription ("");
		}
	}


	private IEnumerator parseDescription(Node node){
		string url = "https://en.wikipedia.org/w/api.php?format=json&action=query&prop=extracts&exintro=&explaintext=&titles=" + node.title.Replace(" ","%20");
		WWW www = new WWW(url);
		yield return www;

		JSONNode N = JSON.Parse(www.text);
		string desc = N["query"]["pages"][0]["extract"];

		//set and call render again
		node.desc = desc;
		setDescription (node.desc);
	}

	private void setTitle (string title)
	{
		transform.Find ("Header").GetComponent<Text> ().text = title;
	}

	private void setDescription (string description)
	{
		transform.Find ("Text").GetComponent<Text> ().text = description;
	}

	private void toggle (bool visible)
	{
		transform.Find ("Header").GetComponent<Text> ().enabled = visible;
		transform.Find ("Text").GetComponent<Text> ().enabled = visible;
		gameObject.GetComponent<Image> ().enabled = visible;

	}







}
