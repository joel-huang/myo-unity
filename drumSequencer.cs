using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Pose = Thalmic.Myo.Pose;

public class drumSequencer : MonoBehaviour {

	// 8-step drum sequencer.

	// controls: macro 1-8, Rate

	// OFF state is when the macro is at control value 0.
	// ON state ranges from control values 0 to 16 which are macro values -1 to 15.
	// begin: set all macros at 0 (-1 st).
	// play while off: circles scale but no sound, macros still at 0.
	// listen for fist gesture, which will turn the circle white and set the state to ON by assigning MIDI CC that is mapped to the macro. The value is 0.
	// When ON, text mesh changed to "Kick".
	// Update dummy text: rotate your fist to change the drum instrument

	// stretch: scale all circles according to beat

	public GameObject myo = null;
	public bool isDrumsOn = false;
	public MidiChannel channel = MidiChannel.Ch1;
	public List<int> macroControllerList = new List<int> {102, 103, 104, 105, 106, 107, 108, 109};
	// V1
//	public List<string> core808 = new List<string> {"Kick 808", "Rim 808", "Snare 808", "Clap 808",
//		"Clave 808", "Tom Low 808", "Hi-hat Closed 808", "Tom Mid 808",
//		"Maracas 808", "Tom Hi 808", "Hi-hat Open 808", "Conga Low 808",
//		"Conga Mid 808", "Cymbal 808", "Conga Hi 808", "Cowbell 808"};
//

	public List<string> core808 = new List<string> {"Mute", "Kick", "Rim", "Snare", "Clap", "Snare b", "Kick b",
		"HH Closed", "Tom Low", "HH Closed b", "Tom Mid Low", "HH Open", "Tom Mid", "Tom Hi", "Crash", "Ride"};
	
	private Pose previousGesture;
	private int transposeCounter = 0;
	private int beatNumberToBePassed;
	private float controlValueToBePassed;

	public Vector3 vectorOnEnteringBeat;
	public Vector3 currentVectorOfBeat;
	public Vector3 previousFinalRotationOfBeat;
	public float knobRotateScale = 5.0f;

	public float scaleTime = 0.3f;
	public float scale = 1.18f;
	private int count = 0;
	private Vector3 originalScale;
	private string nodeName;

	void Start () {
		StartCoroutine (Warmup ());
	}
			

	public void entered (CircleCollider2D coll) {

		nodeName = coll.gameObject.name;

		if (count == 0) {
			originalScale = coll.transform.localScale;
		}

		if (coll.gameObject.name == nodeName) {
			Vector3 targetScale = new Vector3 (scale, scale, 0);
			StartCoroutine (startLerp (coll.transform.localScale, targetScale, coll));
			count = 1;
		}
	}


	public void exited (CircleCollider2D coll) {
		nodeName = coll.gameObject.name;

		if (coll.gameObject.name == nodeName) {
			StartCoroutine (startLerp (coll.transform.localScale, originalScale, coll));
		}
	}

	IEnumerator startLerp (Vector3 currentScale, Vector3 targetScale, CircleCollider2D coll) {

		float time = scaleTime;
		float originalTime = time;

		while (time > 0.0f) {
			time -= Time.deltaTime;
			coll.transform.localScale = Vector3.Lerp(targetScale, currentScale, time / originalTime);
			yield return null;
		}
	}

	public void StayOnSequencer (CircleCollider2D coll) {

		// Obtain all the variables and objects we need.

		// References
		ThalmicMyo thalmicMyo = myo.GetComponent<ThalmicMyo> ();
		GameObject cur = GameObject.FindGameObjectWithTag ("Cursor");

		// current gameObject hovered on: the beat object.
		GameObject currentBeatHovered = coll.gameObject;

		// Get the difference between the current rotation direction and the direction of rotation when the collider was entered.
		// Multiply by factor knobRotateScale.
		Vector3 rotationFromEnteredAngle = new Vector3 (0, 0, (cur.transform.rotation.eulerAngles.z - vectorOnEnteringBeat.z) * knobRotateScale);

		// Calculate the final orientation of the beat - giving a euler z of +/- 360 degrees.
		Vector3 finalRotationOfBeat = currentVectorOfBeat + rotationFromEnteredAngle;

		// Clamp the angle of rotation. Calculate the control value percentage to be sent.
		Vector3 clampedFinalRotationOfBeat = new Vector3 (0, 0, ClampAngle (finalRotationOfBeat.z, -127f, 128f));
		float controlValueFloat = ((-clampedFinalRotationOfBeat.z + 127f) / 256f);

		// Route the control value int to the UI as well.


		int controlValue = Mathf.RoundToInt (controlValueFloat * 16f) - 1;

		// What is the current beat number?
		string beatNumberString = coll.gameObject.name.Substring (coll.gameObject.name.Length - 1);
		int beatNumber = int.Parse(beatNumberString) - 1;

		// Reference the beat text canvas to access the text fields.
		GameObject beatTextCanvas = GameObject.FindGameObjectWithTag ("Beat text canvas drums");
		TextMesh[] beatTextArray = beatTextCanvas.GetComponentsInChildren<TextMesh>();


		//listen for a change in gesture
		if (previousGesture != thalmicMyo.pose) {

			if (thalmicMyo.pose == Pose.Fist) {

				// turn on if fist made

				if (isDrumsOn == false) {
					isDrumsOn = true;
					transposeCounter = 1;
					Sprite fullCircle = Resources.Load <Sprite> ("white_circle_full");
					currentBeatHovered.GetComponent<SpriteRenderer> ().sprite = fullCircle;
				} 

				// else turn off

				else {            
					isDrumsOn = false;
					transposeCounter = 0;
					Sprite emptyCircle = Resources.Load <Sprite> ("white_circle");
					currentBeatHovered.GetComponent<SpriteRenderer> ().sprite = emptyCircle;

				}
			}
		}
			
		// if previous is fist and current is still fist, listen for rolls.
		// allows the user to hold fist and rotate arm to change the drum note
		if (previousGesture == thalmicMyo.pose && thalmicMyo.pose == Pose.Fist) {

			// Store the beatNumber and controlValueFloat (the percentage) in a global var to be sent to the coroutine.
			beatNumberToBePassed = beatNumber;
			controlValueToBePassed = controlValueFloat;

			// Update the drum note text field
			beatTextArray[beatNumber].text = getInstrumentName(controlValueFloat).ToString() + ": " + core808[getInstrumentName(controlValueFloat)];

			// Update the orientation of the beat object, which is used as an 'invisible' knob.
			coll.gameObject.transform.localEulerAngles = clampedFinalRotationOfBeat;

			// Update the previous orientation of the beat object to the current one.
			previousFinalRotationOfBeat = finalRotationOfBeat;

			// Start the control coroutine for Midi CC out.
			if (isDrumsOn == true) {
				StartCoroutine (sendControl (beatNumberToBePassed, controlValueToBePassed));
			}

		}

		previousGesture = thalmicMyo.pose;

		if (isDrumsOn == false) {

			// If the drums aren't on, set the drums to transpose = -1.
			MidiBridge.instance.Warmup ();
			MidiOut.SendControlChange (channel, macroControllerList [beatNumber], 0);
		}

	}

	// Turns all the drums off, by setting all transposes = -1.
	IEnumerator Warmup () {

		yield return new WaitForSeconds (0.2f);

		MidiBridge.instance.Warmup ();

		MidiOut.SendControlChange(channel, macroControllerList[0], 0);
		MidiOut.SendControlChange(channel, macroControllerList[1], 0);
		MidiOut.SendControlChange(channel, macroControllerList[2], 0);
		MidiOut.SendControlChange(channel, macroControllerList[3], 0);
		MidiOut.SendControlChange(channel, macroControllerList[4], 0);
		MidiOut.SendControlChange(channel, macroControllerList[5], 0);
		MidiOut.SendControlChange(channel, macroControllerList[6], 0);
		MidiOut.SendControlChange(channel, macroControllerList[7], 0);
	}

	// Sends the MIDI control message.
	IEnumerator sendControl (int beatNumber, float transposeValue) {

		yield return new WaitForSeconds (0);

		MidiBridge.instance.Warmup ();
		MidiOut.SendControlChange (channel, macroControllerList [beatNumber], transposeValue);
	}

	// Clamp function.
	static float ClampAngle(float angle, float min, float max)
	{
		if (min < 0 && max > 0 && (angle > max || angle < min))	{
			angle -= 360;
			if (angle > max || angle < min) {
				if (Mathf.Abs(Mathf.DeltaAngle(angle, min)) < Mathf.Abs(Mathf.DeltaAngle(angle, max))) return min;
				else return max;
			}
		}

		else if(min > 0 && (angle > max || angle < min)) {
			angle += 360;
			if (angle > max || angle < min) {
				if (Mathf.Abs(Mathf.DeltaAngle(angle, min)) < Mathf.Abs(Mathf.DeltaAngle(angle, max))) return min;
				else return max;
			}
		}

		if (angle < min) return min;
		else if (angle > max) return max;
		else return angle;
	}

	// Given an input controlValueFloat, compute the instrument mapping.
	public static int getInstrumentName(float input){
	
		// Experimental values of when the st's change in Ableton:
		// in the form semitoneNumber: (int)(controlValueFloat*127.0f)
		// 0st: <=4
		// 1st: 5-12
		// 2st: 13-21
		// 3st: 22-29
		// 4st: 30-38
		// 5st: 39-46
		// 6st: 47-54
		// 7st: 55-63
		// 8st: 64-71
		// 9st: 72-80
		// 10st: 81-88
		// 11st: 89-97
		// 12st: 98-105
		// 13st: 106-114
		// 14st: 115-122
		// 15st: 123-126

		int r = (int)(input*127.0f);

		if (r <= 4)
			return 0;
		else if (r >= 5 && r <= 12)
			return 1;
		else if (r >= 13 && r <= 21)
			return 2;
		else if (r >= 22 && r <= 29)
			return 3;
		else if (r >= 30 && r <= 38)
			return 4;
		else if (r >= 39 && r <= 46)
			return 5;
		else if (r >= 47 && r <= 54)
			return 6;
		else if (r >= 55 && r <= 63)
			return 7;
		else if (r >= 64 && r <= 71)
			return 8;
		else if (r >= 72 && r <= 80)
			return 9;
		else if (r >= 81 && r <= 88)
			return 10;
		else if (r >= 89 && r <= 97)
			return 11;
		else if (r >= 98 && r <= 105)
			return 12;
		else if (r >= 106 && r <= 114)
			return 13;
		else if (r >= 115 && r <= 122)
			return 14;
		else if (r >= 123 && r <= 126)
			return 15;
		else
			return 0;

	}

}
