using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Cerebro central del Top-Down Shooter. Maneja la Máquina de Estados Finita (FSM)
/// y transmite los cambios de estado a todos los sistemas (UI, Audio, Spawners) mediante eventos.
///
/// Reglas de Arquitectura:
/// - Singleton con DontDestroyOnLoad.
/// - Ningún otro script puede cambiar Time.timeScale directamente. Todo pasa por acá.
/// - No contiene lógica de UI. La UI debe suscribirse a OnStateChanged para mostrar/ocultar paneles.
/// </summary>
public class GameManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Singleton
    // -------------------------------------------------------------------------
    public static GameManager Instance { get; private set; }

    // -------------------------------------------------------------------------
    // FSM (Finite State Machine)
    // -------------------------------------------------------------------------
    public enum GameState
    {
        MainMenu,
        Playing,
        Pause,
        GameOver,
        Victory
    }

    /// <summary>Estado actual del juego. Solo puede ser modificado internamente.</summary>
    public GameState CurrentState { get; private set; }

    // -------------------------------------------------------------------------
    // Eventos
    // -------------------------------------------------------------------------
    /// <summary>
    /// Se dispara cada vez que el estado cambia.
    /// UIManager, AudioManager y WaveManager deben suscribirse acá.
    /// </summary>
    public static event Action<GameState> OnStateChanged;

    /// <summary>
    /// Fired whenever <see cref="RegisterPlayer"/> is called — including on
    /// respawn. Systems that need an immediate reference to the player
    /// (e.g. EnemyBrain in Tier-3 resolution) can subscribe here instead
    /// of polling every frame.
    /// </summary>
    public static event Action<Transform> OnPlayerRegistered;

    // -------------------------------------------------------------------------
    // Variables Internas
    // -------------------------------------------------------------------------

    // Temporizador de la partida. Solo avanza durante el estado 'Playing'.
    private float _sessionTimer;

    // Guarda el estado en el que estábamos antes de pausar (útil si hay estados extra luego).
    private GameState _stateBeforePause;

    // Flag set by StartNewGame() before reloading the scene so OnSceneLoaded
    // knows it must initialise a fresh session once the new scene is ready.
    private bool _pendingRestart = false;

    // -------------------------------------------------------------------------
    // Session State
    // -------------------------------------------------------------------------

    // PlayerPrefs keys — defined once so UIManager and GameManager share the
    // exact same string without risk of a typo mismatch.
    public const string PrefKeyBestTime = "BestTime";
    public const string PrefKeyMaxCoins = "MaxCoins";

    /// <summary>
    /// True while an active run exists in the loaded scene.
    /// Set to true inside <see cref="OnSceneLoaded"/> after a scene restart,
    /// and to false when the session ends (GameOver or Victory).
    /// Used by <see cref="UIManager"/> to gate the "Continue" button.
    /// </summary>
    public bool HasActiveSession { get; private set; }

    /// <summary>Total coins collected in the current session.</summary>
    public int CurrentCoins { get; private set; }

    /// <summary>
    /// Increments the session coin counter by <paramref name="amount"/>.
    /// Called by <see cref="Platformer.Player.PlayerCollectibles"/> on every pickup.
    /// </summary>
    public void AddCoin(int amount = 1)
    {
        CurrentCoins += amount;
        Debug.Log($"[GameManager] Monedas en sesión: {CurrentCoins}");
    }

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------
    private void Awake()
    {
        // Protección del Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Estado inicial explícito para evitar lecturas de valores nulos al arrancar.
        CurrentState = GameState.MainMenu;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// Fired by Unity after every scene load completes — including the reload
    /// triggered by <see cref="StartNewGame"/>.
    /// When <see cref="_pendingRestart"/> is set, this is the earliest safe moment
    /// to initialise game state, because all scene objects are fully awake.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!_pendingRestart) return;

        _pendingRestart  = false;
        _sessionTimer    = 0f;
        CurrentCoins     = 0;       // Reset coin counter for fresh session
        HasActiveSession = true;

        // Ensure time is running before broadcasting the Playing state.
        Time.timeScale = 1f;
        ChangeState(GameState.Playing);

        Debug.Log("[GameManager] Scene fully loaded — nueva sesión iniciada.");
    }

    private void Update()
    {
        // El tiempo de sesión solo avanza si estamos jugando activamente.
        if (CurrentState == GameState.Playing)
        {
            _sessionTimer += Time.deltaTime;
        }
    }

    // -------------------------------------------------------------------------
    // API Pública de Control de Estados
    // -------------------------------------------------------------------------

    /// <summary>
    /// Transiciona la FSM a un nuevo estado y notifica a los suscriptores.
    /// También maneja el congelamiento del tiempo al pausar.
    /// </summary>
    public void ChangeState(GameState newState)
    {
        if (CurrentState == newState)
        {
            Debug.LogWarning($"[GameManager] Intento de cambiar al estado actual ({newState}). Ignorado.");
            return;
        }

        // --- Manejo del TimeScale ---
        // Freeze on Pause, GameOver, or Victory; unfreeze for everything else.
        switch (newState)
        {
            case GameState.Pause:
                _stateBeforePause = CurrentState;
                Time.timeScale = 0f;
                break;

            case GameState.Victory:
                HasActiveSession = false;
                Time.timeScale = 0f;
                TrySaveRecords();   // ← persist best time & max coins
                break;

            case GameState.GameOver:
                HasActiveSession = false;
                Time.timeScale = 0f;
                break;

            default:
                Time.timeScale = 1f;
                break;
        }

        // --- Transición ---
        GameState previous = CurrentState;
        CurrentState = newState;

        Debug.Log($"[GameManager] Cambio de Estado: {previous} → {CurrentState}");

        // Dispara el evento para que los demás scripts reaccionen
        OnStateChanged?.Invoke(CurrentState);
    }

    /// <summary>
    /// Inicia una nueva partida desde cero.
    /// Ideal para llamar desde el botón "Jugar" en el Main Menu o "Reintentar" en Game Over.
    /// </summary>
    public void StartNewGame()
    {
        // Ensure time runs during the scene load so Unity's async work is not
        // blocked (some scene activation logic requires unscaled time).
        Time.timeScale = 1f;

        // Mark pending restart BEFORE LoadScene so OnSceneLoaded fires correctly
        // even on scenes that load extremely fast (single-frame load).
        _pendingRestart = true;

        Debug.Log("[GameManager] Recargando escena para nueva partida...");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Resumes an in-progress run from the Main Menu without reloading the scene.
    /// Only valid when <see cref="HasActiveSession"/> is true.
    /// </summary>
    public void ContinueGame()
    {
        if (!HasActiveSession)
        {
            Debug.LogWarning("[GameManager] ContinueGame llamado sin sesión activa. Ignorado.");
            return;
        }

        ChangeState(GameState.Playing);
    }

    /// <summary>
    /// Vuelve al estado guardado antes de pausar.
    /// </summary>
    public void ResumeFromPause()
    {
        if (CurrentState != GameState.Pause)
        {
            Debug.LogWarning("[GameManager] ResumeFromPause llamado, pero el juego no está pausado.");
            return;
        }

        ChangeState(_stateBeforePause);
    }

    /// <summary>
    /// Devuelve el juego al Menú Principal y limpia el entorno.
    /// </summary>
    public void ReturnToMainMenu()
    {
        _sessionTimer = 0f;
        Time.timeScale = 1f;
        ChangeState(GameState.MainMenu);
    }

    // -------------------------------------------------------------------------
    // Accesos Públicos
    // -------------------------------------------------------------------------

    /// <summary>Devuelve los segundos transcurridos en la partida actual.</summary>
    public float SessionTime => _sessionTimer;

    // -------------------------------------------------------------------------
    // Records — PlayerPrefs
    // -------------------------------------------------------------------------

    /// <summary>
    /// Evaluates the just-completed session against saved records and
    /// overwrites them when the player sets a new best.
    /// <para>Best Time  = lowest value (faster run wins).</para>
    /// <para>Max Coins  = highest value (more coins wins).</para>
    /// Called exclusively from <see cref="ChangeState"/> on Victory.
    /// </summary>
    private void TrySaveRecords()
    {
        // ── Best Time (lower is better) ───────────────────────────────────────
        // GetFloat returns the default (3rd arg) when the key doesn't exist,
        // so float.MaxValue forces a save on the very first victory.
        float savedBestTime = PlayerPrefs.GetFloat(PrefKeyBestTime, float.MaxValue);

        if (_sessionTimer < savedBestTime)
        {
            PlayerPrefs.SetFloat(PrefKeyBestTime, _sessionTimer);
            Debug.Log($"[GameManager] ¡Nuevo mejor tiempo! {FormatTime(_sessionTimer)} " +
                      $"(anterior: {(savedBestTime == float.MaxValue ? "--:--" : FormatTime(savedBestTime))})");
        }

        // ── Max Coins (higher is better) ─────────────────────────────────────
        int savedMaxCoins = PlayerPrefs.GetInt(PrefKeyMaxCoins, 0);

        if (CurrentCoins > savedMaxCoins)
        {
            PlayerPrefs.SetInt(PrefKeyMaxCoins, CurrentCoins);
            Debug.Log($"[GameManager] ¡Nuevo máximo de monedas! {CurrentCoins} " +
                      $"(anterior: {savedMaxCoins})");
        }

        // Flush to disk immediately so data is not lost on a crash.
        PlayerPrefs.Save();
    }

    // -------------------------------------------------------------------------
    // Utilidades de Formato
    // -------------------------------------------------------------------------

    /// <summary>
    /// Converts a raw float (seconds) into a "MM:SS" display string.
    /// Shared by GameManager and UIManager to guarantee identical formatting.
    /// </summary>
    public static string FormatTime(float totalSeconds)
    {
        int minutes = Mathf.FloorToInt(totalSeconds / 60f);
        int seconds = Mathf.FloorToInt(totalSeconds % 60f);
        return $"{minutes:00}:{seconds:00}";
    }

    // -------------------------------------------------------------------------
    // Player Registry (FIX-2)
    // -------------------------------------------------------------------------

    /// <summary>
    /// The current player's Transform, registered at runtime by
    /// <see cref="PlayerRegistration"/> via <see cref="RegisterPlayer"/>.
    /// <para>
    /// Read-only to all external systems. Only <see cref="RegisterPlayer"/>
    /// and <see cref="UnregisterPlayer"/> may write this value, ensuring
    /// a single, authoritative reference that survives scene reloads,
    /// respawns, and arbitrary enemy spawn order.
    /// </para>
    /// </summary>
    public Transform PlayerTransform { get; private set; }

    /// <summary>
    /// Called by <see cref="PlayerRegistration"/> (attached to the Player
    /// prefab) in Awake/Start to publish the player's Transform.
    /// Safe to call multiple times: respawning with a new instance simply
    /// replaces the old reference and fires <see cref="OnPlayerRegistered"/>
    /// again so all subscribers (EnemyBrain, minimap, etc.) update.
    /// </summary>
    /// <param name="player">The player's root Transform. Must not be null.</param>
    public void RegisterPlayer(Transform player)
    {
        if (player == null)
        {
            Debug.LogError("[GameManager] RegisterPlayer called with a null Transform. " +
                           "Check the PlayerRegistration component.");
            return;
        }

        PlayerTransform = player;
        Debug.Log($"[GameManager] Player registered: '{player.name}'.");

        // Notify all subscribers (e.g. EnemyBrain.WaitForPlayer coroutines)
        // that a valid player reference is now available.
        OnPlayerRegistered?.Invoke(PlayerTransform);
    }

    /// <summary>
    /// Called when the player is permanently removed (game over, not respawning).
    /// Clears the reference so enemies fall back to idle safely.
    /// </summary>
    public void UnregisterPlayer()
    {
        if (PlayerTransform == null)
        {
            Debug.LogWarning("[GameManager] UnregisterPlayer called but no player was registered.");
            return;
        }

        Debug.Log($"[GameManager] Player '{PlayerTransform.name}' unregistered.");
        PlayerTransform = null;
    }
}