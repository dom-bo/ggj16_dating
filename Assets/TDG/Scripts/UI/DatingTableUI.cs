﻿using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Linq;

public class DatingTableUI : MonoBehaviour 
{
	public Button quitButton;
	public Button boozeButton;

	public CanvasGroup playDeckCanvasGroup;
	public Transform playerDeckContentPanel;
	public GameObject cardPrefab;

	public SpeechBubble speechBuble;
	public SpeechBubble dateSpeechBuble;
	public RatingPanel ratingPanel;

	public CharacterAnimationController animController;

	public ParticleSystem drinkBoozeParticles;

	GameManager GameManager { get { return GameManager.Instance; } }

	Player Player { get { return GameManager.Player; } }

	void Awake ()
	{
		quitButton.onClick.AddListener(HandleFleeButton);
		boozeButton.onClick.AddListener(DrinkBooze);
		dateSpeechBuble.bubbleClosed += PlayerMoveStarts;
		speechBuble.bubbleClosed += PlayerMoveOver;
	}

	void OnDestroy ()
	{
		GameManager.OnDatePlaysCard -= HandleDatePlaysCard;
		GameManager.OnDateDrinks -= HandleDateDrinks;
		GameManager.OnDateFlees -= HandleDateFlees;
		GameManager.OnDatePassesOut -= HandleDatePassesOut;
	}

	void Start ()
	{
		PopulateMainDeck();
		if (!Player.startsPhase) {
			LockPlayerCards();	
		}

		GameManager.PauseWebsocketListener(false);
		GameManager.OnDatePlaysCard += HandleDatePlaysCard;
		GameManager.OnDateDrinks += HandleDateDrinks;
		GameManager.OnDateFlees += HandleDateFlees;
		GameManager.OnDatePassesOut += HandleDatePassesOut;
	}

	void PopulateMainDeck ()
	{		
		for (int i = 0; i < playerDeckContentPanel.childCount; i++) {
			Destroy(playerDeckContentPanel.GetChild(i).gameObject);
		}
		foreach (var card in Player.cards.Where(c => !c.used)) {
			var cardUI = CreateCard(card, playerDeckContentPanel);
			var cardToCreate = card;
			cardUI.cardButton.onClick.AddListener(() => HandlePlayerDeckCardClicked(cardToCreate));
			cardUI.cardButton.onClick.AddListener(() => Destroy(cardUI.gameObject));
		}
	}

	CardUI CreateCard (Card card, Transform contentPanel)
	{
		var cardGo = Instantiate(cardPrefab);
		cardGo.transform.SetParent(contentPanel, false);
		var cardUI = cardGo.GetComponent<CardUI>();
		cardUI.Init(card);

		return cardUI;
	}

	#region Gameplay

	void HandlePlayerDeckCardClicked (Card card)
	{
		PlayCard(card);
		card.used = true;;
	}

	void PlayCard (Card card)
	{
		LockPlayerCards();
		var cardText = GameManager.ChooseTextForCard(card);
		var texts = card.positive ? cardText.good : cardText.bad;
		var index = RandomHelper.Next(texts.Length);
		var text = texts[index];
		GameManager.SendPlayCard(card, cardText, index);
		DisplayText(text, speechBuble);
	}

	void DisplayText (string text, SpeechBubble bubble)
	{
		bubble.DisplayText(text);
	}

	void PlayerMoveStarts ()
	{
		UnlockPlayerCards();
		if (Player.startsPhase && Player.cards.TrueForAll(c => c.used)) {
			ratingPanel.Show();
		}
	}

	void PlayerMoveOver ()
	{
		if (!Player.startsPhase && Player.cards.TrueForAll(c => c.used)) {
			ratingPanel.Show();
		}
	}

	void UnlockPlayerCards ()
	{
		boozeButton.interactable = true;
		playDeckCanvasGroup.alpha = 1;
		playDeckCanvasGroup.interactable = true;
	}

	void LockPlayerCards ()
	{
		boozeButton.interactable = false;
		playDeckCanvasGroup.alpha = .5f;
		playDeckCanvasGroup.interactable = false;
	}

	#endregion

	#region Booze

	void DrinkBooze ()
	{
		GameManager.PlayerDrinks();
		PopulateMainDeck();
		StartCoroutine(DrinkBoozeRoutine());
	}

	IEnumerator DrinkBoozeRoutine ()
	{
		LockPlayerCards();

		drinkBoozeParticles.gameObject.SetActive(true);
		drinkBoozeParticles.Play();

		yield return new WaitForSeconds(4f);

		drinkBoozeParticles.gameObject.SetActive(false);
		drinkBoozeParticles.Stop();

		if (Player.boozeLevel >= GameManager.maxBoozeLevel) {
			GameManager.CurrentState = GameManager.GameState.PlayerPassesOut;
			GameManager.SendPassOut();
			GameManager.CloseWebsocket();
			SceneManager.LoadScene(MainGameController.SCENE_THE_DECISION);
		}

		UnlockPlayerCards();
	}

	#endregion

	void HandleFleeButton ()
	{
		GameManager.CurrentState = GameManager.GameState.PlayerFlees;
		GameManager.SendFlee();
		GameManager.CloseWebsocket();
		SceneManager.LoadScene(MainGameController.SCENE_THE_DECISION);
	}		

	#region other player

	void HandleDatePlaysCard (PlayCardPayload playCardPayload)
	{
		var card = GameManager.GetCard(playCardPayload.card);
		var cardText = GameManager.GetCardText(playCardPayload.text);
		var texts = playCardPayload.positive ? cardText.good : cardText.bad;
		var text = texts[playCardPayload.index];
		DisplayText(text, dateSpeechBuble);

		animController.PlayCard(card, AnimationEndsCallback);
	}

	void HandleDateDrinks (DrinkBoozePayload drinkBoozePayload)
	{	
		animController.PlayDrinkBoozeAnimation(null);	
		if (drinkBoozePayload.boozeLevel > GameManager.maxBoozeLevel) {
			
			GameManager.CurrentState = GameManager.GameState.DatePassesOut;
			animController.PlayPassoutAnimation(LoadDecision);
		}
	}

	void HandleDateFlees ()
	{
		GameManager.CurrentState = GameManager.GameState.DateFlees;
		animController.PlayFleeAnimation(LoadDecision);
	}		

	void HandleDatePassesOut () {
		GameManager.CurrentState = GameManager.GameState.DatePassesOut;
		animController.PlayPassoutAnimation(LoadDecision);
	}

	#endregion

	void AnimationEndsCallback ()
	{
		// TODO 
		Debug.LogWarning(">>>> Animation finished"); 
	}

	void LoadDecision ()
	{
		SceneManager.LoadScene(MainGameController.SCENE_THE_DECISION);
	}
}
