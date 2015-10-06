using UnityEngine;
using System.Collections;

public class COM : MonoBehaviour {

	// Use this for initialization
	public static string com;
	public UnityEngine.UI.InputField Field;
	
	// Update is called once per frame
	void Update () {
		com = Field.text;
	}
}
