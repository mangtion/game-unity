// game-unity/Assets/Scripts/UIController.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace YiSunSin
{
    public class UIController : MonoBehaviour
    {
        public GameManager Manager;
        public PlayerController Player;

        public GameObject TitleScreen;
        public GameObject PauseScreen;
        public GameObject LevelUpScreen;
        public GameObject GameOverScreen;
        public GameObject WinScreen;
        public GameObject Hud;

        public Transform UpgradeCardContainer;
        public GameObject UpgradeCardPrefab; // a Button + two Text children, built by ProjectBootstrap
        public Text GameOverStatsText;
        public Text WinStatsText;
        public Text HpBarText; // simple text-based HUD; a fill-bar Image is wired the same way if preferred
        public Text TimerText;

        GameStatus lastBuiltStatus = (GameStatus)(-1); // sentinel: forces the first real build
        List<UpgradeDefinition> lastBuiltChoices;

        void OnEnable() => Manager.OnStatusChanged += HandleStatusChanged;
        void OnDisable() => Manager.OnStatusChanged -= HandleStatusChanged;

        void Start() => HandleStatusChanged(Manager.Status); // build the initial Title screen once

        void Update()
        {
            if (Manager.Status == GameStatus.Playing || Manager.Status == GameStatus.PausedManual)
            {
                Vector2 dir = Vector2.zero;
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) dir.y += 1f;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) dir.y -= 1f;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) dir.x -= 1f;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) dir.x += 1f;
                Player.SetInput(dir);
            }

            if (Input.GetKeyDown(KeyCode.Escape) &&
                (Manager.Status == GameStatus.Playing || Manager.Status == GameStatus.PausedManual))
            {
                Manager.TogglePause();
            }

            if (Manager.Status == GameStatus.Playing)
            {
                if (HpBarText != null) HpBarText.text = $"HP {Player.Hp:0}/{Player.MaxHp:0}";
                if (TimerText != null)
                {
                    float remaining = Mathf.Max(0f, GameConfig.WinTime - Manager.Elapsed);
                    int mm = Mathf.FloorToInt(remaining / 60f);
                    int ss = Mathf.FloorToInt(remaining % 60f);
                    TimerText.text = $"{mm:00}:{ss:00}";
                }
            }
        }

        void HandleStatusChanged(GameStatus status)
        {
            // A multi-level-up pickup (Task 10) re-enters PausedLevelUp with a NEW UpgradeChoices
            // list without the status value itself changing — GameManager.ChooseUpgrade fires
            // OnStatusChanged again in that case specifically so this method re-runs. Detect that
            // case via reference-identity on UpgradeChoices, not just the status enum, or the
            // second (and later) card sets from one multi-level pickup would never render.
            bool statusChanged = status != lastBuiltStatus;
            bool choicesChanged = status == GameStatus.PausedLevelUp && Manager.UpgradeChoices != lastBuiltChoices;
            if (!statusChanged && !choicesChanged) return;

            lastBuiltStatus = status;

            TitleScreen.SetActive(status == GameStatus.Title);
            PauseScreen.SetActive(status == GameStatus.PausedManual);
            LevelUpScreen.SetActive(status == GameStatus.PausedLevelUp);
            GameOverScreen.SetActive(status == GameStatus.GameOver);
            WinScreen.SetActive(status == GameStatus.Win);
            Hud.SetActive(status == GameStatus.Playing || status == GameStatus.PausedManual || status == GameStatus.PausedLevelUp);

            if (status == GameStatus.PausedLevelUp)
            {
                lastBuiltChoices = Manager.UpgradeChoices;
                BuildUpgradeCards();
            }
            else if (status == GameStatus.GameOver)
            {
                GameOverStatsText.text = FormatStats();
            }
            else if (status == GameStatus.Win)
            {
                WinStatsText.text = FormatStats();
            }
        }

        void BuildUpgradeCards()
        {
            for (int i = UpgradeCardContainer.childCount - 1; i >= 0; i--)
                Destroy(UpgradeCardContainer.GetChild(i).gameObject);

            foreach (var upgrade in Manager.UpgradeChoices)
            {
                var cardGo = Instantiate(UpgradeCardPrefab, UpgradeCardContainer);
                var texts = cardGo.GetComponentsInChildren<Text>();
                texts[0].text = upgrade.Name;
                texts[1].text = upgrade.Description;
                string capturedId = upgrade.Id; // avoid closure-over-loop-variable bug
                cardGo.GetComponent<Button>().onClick.AddListener(() => Manager.ChooseUpgrade(capturedId));
            }
        }

        string FormatStats()
        {
            float survived = Mathf.Min(Manager.Elapsed, GameConfig.WinTime);
            int mm = Mathf.FloorToInt(survived / 60f);
            int ss = Mathf.FloorToInt(survived % 60f);
            return $"생존 시간: {mm:00}:{ss:00} · 도달 레벨: {Player.Level}";
        }

        // Wired to the Title screen's Start button and the GameOver/Win screens' Restart buttons.
        public void OnStartClicked() => Manager.StartGame();
        public void OnRestartClicked() => Manager.Restart();
    }
}
