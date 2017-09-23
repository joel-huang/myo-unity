using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ccBandPassFilter : MonoBehaviour {

	// Define the MIDI channel.
	public MidiChannel channel = MidiChannel.Ch1;
	public GameObject myo = null;
	int controlNumber = 22;
	float previousValue = 0.0f;

	void Update ()
	{
		float value = float.Parse(GetComponentInChildren<TextMesh> ().text) / 100f;
		if (value != previousValue) {
			StartCoroutine (controlChange (value));
		}

		previousValue = value;

	}

	IEnumerator controlChange (float value) 
	{
		MidiBridge.instance.Warmup ();
		MidiOut.SendControlChange (channel, controlNumber, value);
		yield return new WaitForSeconds (0);
	}
}
