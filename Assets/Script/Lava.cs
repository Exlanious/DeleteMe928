using UnityEngine;

public class Lava : MonoBehaviour
{
    [SerializeField] private float riseSpeed = 0.5f;
    [SerializeField] private Transform player;
    [SerializeField] private float hearingRadius = 30f;
    [SerializeField] private AudioClip lavaAudioClip;
    [SerializeField, Range(0f, 1f)] private float audioVolume = 1f;

    private AudioSource lavaAudioSource;

    private void Awake()
    {
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
        transform.position += Vector3.up * riseSpeed * Time.deltaTime;

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
        if (playerMovement != null)
        {
            playerMovement.ReturnToStart();
        }
    }
}
