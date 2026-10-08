using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class Lava : MonoBehaviour
{
    [SerializeField] private float riseSpeed = 0.5f;
    [SerializeField, Min(0f)] private float catchUpDistance = 10f;
    [SerializeField, Min(0f)] private float catchUpSpeedPerUnit = 0.5f;
    [SerializeField, Min(0f)] private float maximumRiseSpeed = 8f;
    [SerializeField] private Transform player;
    [SerializeField] private float hearingRadius = 30f;
    [SerializeField] private AudioClip lavaAudioClip;
    [SerializeField, Range(0f, 1f)] private float audioVolume = 1f;
    [SerializeField] private AudioClip deathAudioClip;
    [SerializeField, Range(0f, 1f)] private float deathAudioVolume = 1f;

    private AudioSource lavaAudioSource;
    private AudioSource deathAudioSource;
    private Collider lavaCollider;
    private bool isReloading;

    private void Awake()
    {
        lavaCollider = GetComponent<Collider>();
        lavaAudioSource = GetComponent<AudioSource>();
        if (lavaAudioSource == null)
        {
            lavaAudioSource = gameObject.AddComponent<AudioSource>();
        }

        lavaAudioSource.clip = lavaAudioClip;
        lavaAudioSource.loop = true;
        lavaAudioSource.playOnAwake = false;
        lavaAudioSource.spatialBlend = 1f;
        lavaAudioSource.rolloffMode = AudioRolloffMode.Linear;

        deathAudioSource = gameObject.AddComponent<AudioSource>();
        deathAudioSource.playOnAwake = false;
        deathAudioSource.spatialBlend = 0f;
    }

    private void Start()
    {
        if (player == null)
        {
            PlayerMovement playerMovement = FindAnyObjectByType<PlayerMovement>();
            if (playerMovement != null)
            {
                player = playerMovement.transform;
            }
        }
    }

    private void Update()
    {
        Vector3 position = transform.position;
        if (player != null)
        {
            position.x = player.position.x;
            position.z = player.position.z;
        }

        float lavaSurfaceHeight = lavaCollider != null ? lavaCollider.bounds.max.y : position.y;
        float heightAboveLava = player != null ? Mathf.Max(0f, player.position.y - lavaSurfaceHeight) : 0f;
        float catchUpSpeed = Mathf.Max(0f, heightAboveLava - catchUpDistance) * catchUpSpeedPerUnit;
        float currentRiseSpeed = Mathf.Min(maximumRiseSpeed, riseSpeed + catchUpSpeed);
        position.y += currentRiseSpeed * Time.deltaTime;
        transform.position = position;

        lavaAudioSource.maxDistance = hearingRadius;
        lavaAudioSource.volume = audioVolume;
        if (player == null || lavaAudioClip == null || hearingRadius <= 0f)
        {
            if (lavaAudioSource.isPlaying)
            {
                lavaAudioSource.Stop();
            }
            return;
        }

        bool playerCanHearLava = (player.position - transform.position).sqrMagnitude <= hearingRadius * hearingRadius;
        if (playerCanHearLava && !lavaAudioSource.isPlaying)
        {
            lavaAudioSource.Play();
        }
        else if (!playerCanHearLava && lavaAudioSource.isPlaying)
        {
            lavaAudioSource.Stop();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerMovement playerMovement = other.GetComponentInParent<PlayerMovement>();
        if (playerMovement == null || isReloading)
        {
            return;
        }

        isReloading = true;
        if (lavaAudioSource.isPlaying)
        {
            lavaAudioSource.Stop();
        }
        StartCoroutine(PlayDeathSoundAndReload());
    }

    private IEnumerator PlayDeathSoundAndReload()
    {
        if (deathAudioClip != null)
        {
            deathAudioSource.PlayOneShot(deathAudioClip, deathAudioVolume);
            yield return new WaitForSecondsRealtime(deathAudioClip.length);
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
