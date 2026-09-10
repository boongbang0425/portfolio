using UnityEngine;

public class BoardSwitcher : MonoBehaviour
{
	[SerializeField] GameObject pauseBoard;
	[SerializeField] GameObject optionsBoard;

	void Awake()
	{
		ShowPause();
	}

	public void ShowPause()
	{
		if (pauseBoard != null) pauseBoard.SetActive(true);
		if (optionsBoard != null) optionsBoard.SetActive(false);
	}

	public void ShowOptions()
	{
		if (pauseBoard != null) pauseBoard.SetActive(false);
		if (optionsBoard != null) optionsBoard.SetActive(true);
	}
}