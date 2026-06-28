using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Maneja los sliders del menú de opciones de forma independiente.
/// Adaptado versión "Express" para no depender de un AudioManager externo.
/// </summary>
public class UI_OptionsMenu : MonoBehaviour
{
    // Claves locales para guardar en PlayerPrefs sin depender de otro script
    private const string PREF_KEY_MUSIC = "MusicVolume";
    private const string PREF_KEY_SFX = "SFXVolume";

    [Header("Volume Sliders")]
    [Tooltip("Controla el volumen global del juego.")]
    [SerializeField] private Slider _musicSlider;

    [Tooltip("Slider de SFX (Preparado para el futuro).")]
    [SerializeField] private Slider _sfxSlider;

    private void Start()
    {
        // 1. Leer preferencias guardadas (por defecto 1 = volumen máximo)
        if (_musicSlider != null)
        {
            float savedMusic = PlayerPrefs.GetFloat(PREF_KEY_MUSIC, 1f);
            _musicSlider.value = savedMusic;
            // Aplicar el volumen global inicial
            AudioListener.volume = savedMusic;

            // Asignar el listener DESPUÉS de cambiar el valor inicial
            _musicSlider.onValueChanged.AddListener(OnMusicSliderChanged);
        }

        if (_sfxSlider != null)
        {
            float savedSFX = PlayerPrefs.GetFloat(PREF_KEY_SFX, 1f);
            _sfxSlider.value = savedSFX;
            _sfxSlider.onValueChanged.AddListener(OnSFXSliderChanged);
        }
    }

    private void OnDestroy()
    {
        // Limpieza de eventos para evitar errores si el objeto se destruye
        if (_musicSlider != null)
            _musicSlider.onValueChanged.RemoveListener(OnMusicSliderChanged);

        if (_sfxSlider != null)
            _sfxSlider.onValueChanged.RemoveListener(OnSFXSliderChanged);
    }

    /// <summary>
    /// Se llama cuando se mueve el slider principal.
    /// Modifica el volumen maestro de todo el juego instantáneamente.
    /// </summary>
    private void OnMusicSliderChanged(float value)
    {
        PlayerPrefs.SetFloat(PREF_KEY_MUSIC, value);

        // Hack rápido y efectivo: controla el volumen global de Unity
        AudioListener.volume = value;
    }

    /// <summary>
    /// Se llama cuando se mueve el slider de SFX.
    /// </summary>
    private void OnSFXSliderChanged(float value)
    {
        PlayerPrefs.SetFloat(PREF_KEY_SFX, value);
        // Si más adelante agregas sonidos individuales, los conectas acá.
    }
}