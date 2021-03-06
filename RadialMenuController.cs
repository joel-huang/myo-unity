using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class RadialMenuController : MonoBehaviour
{
	List<Button> childButtons = new List<Button> (); // list of buttons, not using array because list is more powerful
	Vector2[] buttonGoalPos;
	bool open = false; // menu open or closed state
	int buttonDistance = 150; // distance between buttons
	float speed = 2.0f;

	void Start()
	{
		// get all button components, filter out the parent button (menu open button), add the rest to the list
		childButtons = this.GetComponentsInChildren<Button> (true).Where (x => x.gameObject.transform.parent != transform.parent).ToList ();
		buttonGoalPos = new Vector2[childButtons.Count];

		// automatically assign menu open button's onClick function by adding a listener
		this.GetComponent<Button>().onClick.AddListener( () => {OpenMenu();} );

		// automatically centre pivot point rather than using the corner
		this.GetComponent<RectTransform>().pivot = new Vector2(0.5f,0.5f);

		foreach (Button b in childButtons) {
			b.gameObject.transform.position = this.transform.position;
//			Color col = b.gameObject.GetComponent<Image> ().color;
//			col.r = 1;
//			col.g = 1;
//			col.b = 1;
//			col.a = 0;
			b.gameObject.SetActive (false);
		}
	}

	public void OpenMenu()
	{
		open = !open; // set open boolean to not open, like a switch

		float angle = 90 / (childButtons.Count-1) * Mathf.Deg2Rad; // 90 deg sweep
		for (int i = 0; i < childButtons.Count; i++) // loop through the buttons and assign their positions 
		{
			if (open)
			{
				float xpos = Mathf.Cos (angle * i) * buttonDistance;
				float ypos = Mathf.Sin (angle * i) * buttonDistance;

				Debug.Log (i);
				buttonGoalPos[i] = new Vector2 (this.transform.position.x + xpos, this.transform.position.y + ypos); // relative to main menu button
			}

			else
			{
				buttonGoalPos[i] = this.transform.position; // set them all back to the center.
			}
		}

		StartCoroutine (MoveButtons ());
		Debug.Log ("MOVING");
	}

	private IEnumerator MoveButtons() {
		foreach (Button b in childButtons) {
			b.gameObject.SetActive (true);
		}
		int loops = 0;
		while (loops <= buttonDistance / speed) {
			yield return new WaitForSeconds (0.01f);
			for (int i = 0; i < childButtons.Count; i++) {
//				Color c = childButtons [i].gameObject.GetComponent<Image> ().color;
//				if (open) {
//					c.a = Mathf.Lerp (c.a, 1, speed * Time.deltaTime);
//				}
//				else {
//					c.a = Mathf.Lerp (c.a, 0, speed * Time.deltaTime);
//				}
//
//				childButtons [i].gameObject.GetComponent<Image> ().color = c;
				childButtons [i].gameObject.transform.position = Vector2.Lerp (
					childButtons [i].gameObject.transform.position,
					buttonGoalPos [i], speed * Time.deltaTime);
			}

			loops++;
		}
		if (!open) {
			foreach (Button b in childButtons) {
				b.gameObject.SetActive (false);
			}
		}
	}
}