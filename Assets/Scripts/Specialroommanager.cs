using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

// Un solo manager en toda la escena: controla el bloqueo de disparo y el contador
// compartido por todas las habitaciones especiales.
public class SpecialRoomManager : MonoBehaviour
{
    public static SpecialRoomManager Instance { get; private set; }

    // El arma consulta esto antes de disparar
    public static bool ShootingLocked => Instance != null && Instance.lockCount > 0;

    [Header("UI")]
    public TMP_Text timerText;               // texto del contador (en un Canvas)
    public string label = "";                // texto antes del número, por ejemplo "Puertas abren en: "


    private int lockCount;
    private float remaining;
    private bool counting;
    private readonly List<Action> pending = new List<Action>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        SetTimerVisible(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Cada habitación bloqueada suma un "candado" de disparo; al abrirse lo quita
    public void AddLock() { lockCount++; }
    public void RemoveLock() { lockCount = Mathf.Max(0, lockCount - 1); }

    // Cancela todo (al morir el jugador): quita el bloqueo de disparo y apaga el contador
    public void CancelAll()
    {
        lockCount = 0;
        counting = false;
        remaining = 0f;
        pending.Clear();
        SetTimerVisible(false);
    }

    public void StartCountdown(float seconds, Action onFinished)
    {
        remaining = counting ? Mathf.Max(remaining, seconds) : seconds;
        counting = true;
        if (onFinished != null) pending.Add(onFinished);

        SetTimerVisible(true);
        UpdateText();
    }

    void Update()
    {
        if (!counting) return;

        remaining -= Time.deltaTime;
        UpdateText();

        if (remaining <= 0f)
        {
            counting = false;
            SetTimerVisible(false);

            Action[] callbacks = pending.ToArray();
            pending.Clear();
            foreach (Action cb in callbacks) cb?.Invoke();
        }
    }

    void UpdateText()
    {
        if (timerText == null) return;
        int s = Mathf.Max(0, Mathf.CeilToInt(remaining));
        timerText.text = string.Format("{0}{1}:{2:00}", label, s / 60, s % 60);
    }

    void SetTimerVisible(bool visible)
    {
        if (timerText != null) timerText.gameObject.SetActive(visible);
    }
}