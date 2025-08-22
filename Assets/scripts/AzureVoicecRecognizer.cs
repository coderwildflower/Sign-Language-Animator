using UnityEngine;
using Microsoft.CognitiveServices.Speech;
using TMPro;
using UnityEngine.UI;
using System;

public class AzureVoicecRecognizer : MonoBehaviour
{
    [System.Serializable]
    public class PhraseAnimationPair
    {
        public string phrase;
        public string animationName;
        [Range(0f, 1f)]
        public float matchThreshold = 0.8f;
    }

    private object threadLocker = new object();
    private bool waitingForReco;
    private string message;
    private bool micPermissionGranted = false;

    [SerializeField] private string subscriptionKey, region;
    [SerializeField] private Button startRecordButton;
    [SerializeField] private TextMeshProUGUI outputMessageText;
    [SerializeField] private Animator characterAnimator;
    [SerializeField] private PhraseAnimationPair[] phraseAnimationPairs;
    private string pendingRecognizedText; // Store recognized text for main thread processing
    void Start()
    {
        micPermissionGranted = true;
        message = "Click the button to recognize speech";
        startRecordButton.onClick.AddListener(ButtonClick);

    }

    private void Update()
    {

        lock (threadLocker)
        {
            if (startRecordButton != null)
            {
                startRecordButton.interactable = !waitingForReco && micPermissionGranted;
            }
            if (outputMessageText != null)
            {
                outputMessageText.text = message;
            }

            if (!string.IsNullOrEmpty(pendingRecognizedText))
            {
                CheckPhraseMatch(pendingRecognizedText);
                pendingRecognizedText = null;
            }
        }
    }

    public async void ButtonClick()
    {

        var config = SpeechConfig.FromSubscription(subscriptionKey, region);

        using (var recognizer = new SpeechRecognizer(config))
        {
            lock (threadLocker)
            {
                waitingForReco = true;
            }

            var result = await recognizer.RecognizeOnceAsync().ConfigureAwait(false);

            string newMessage = string.Empty;
            if (result.Reason == ResultReason.RecognizedSpeech)
            {
                newMessage = result.Text;

                // Store recognized text to be processed on main thread
                lock (threadLocker)
                {
                    pendingRecognizedText = newMessage;
                }
            }
            else if (result.Reason == ResultReason.NoMatch)
            {
                newMessage = "NOMATCH: Speech could not be recognized.";
            }
            else if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = CancellationDetails.FromResult(result);
                newMessage = $"CANCELED: Reason={cancellation.Reason} ErrorDetails={cancellation.ErrorDetails}";
            }

            lock (threadLocker)
            {
                message = newMessage;
                waitingForReco = false;
            }
        }

    }
    private void CheckPhraseMatch(string recognizedText)
    {
        if (string.IsNullOrEmpty(recognizedText)) return;

        string cleanedRecognizedText = CleanText(recognizedText);

        foreach (var pair in phraseAnimationPairs)
        {
            string cleanedTargetPhrase = CleanText(pair.phrase);

            // Try exact match first
            if (string.Equals(cleanedRecognizedText, cleanedTargetPhrase, StringComparison.OrdinalIgnoreCase))
            {
                TriggerAnimation(pair.animationName, pair.phrase);
                return;
            }

            // Try fuzzy matching using Levenshtein distance
            float similarity = CalculateSimilarity(cleanedRecognizedText, cleanedTargetPhrase);
            if (similarity >= pair.matchThreshold)
            {
                TriggerAnimation(pair.animationName, pair.phrase);
                return;
            }
        }

        Debug.Log($"No matching phrase found for: {recognizedText}");
    }

    private string CleanText(string textinput)
    {
        if (string.IsNullOrEmpty(textinput)) return string.Empty;

        return System.Text.RegularExpressions.Regex.Replace(textinput.ToLower().Trim(), @"[^\w\s]", "")
               .Trim();
    }

    private float CalculateSimilarity(string source, string target)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
            return 0f;

        if (source == target) return 1f;

        int distance = LevenshteinDistance(source, target);
        int maxLength = Mathf.Max(source.Length, target.Length);

        return 1f - ((float)distance / maxLength);
    }

    private int LevenshteinDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source))
            return string.IsNullOrEmpty(target) ? 0 : target.Length;

        if (string.IsNullOrEmpty(target))
            return source.Length;

        int[,] distance = new int[source.Length + 1, target.Length + 1];

        for (int i = 0; i <= source.Length; i++)
            distance[i, 0] = i;

        for (int j = 0; j <= target.Length; j++)
            distance[0, j] = j;

        for (int i = 1; i <= source.Length; i++)
        {
            for (int j = 1; j <= target.Length; j++)
            {
                int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;

                distance[i, j] = Mathf.Min(
                    Mathf.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                    distance[i - 1, j - 1] + cost);
            }
        }

        return distance[source.Length, target.Length];
    }

    private void TriggerAnimation(string animationName, string matchedPhrase)
    {
        if (characterAnimator != null && !string.IsNullOrEmpty(animationName))
        {
            characterAnimator.SetTrigger(animationName);
            Debug.Log($"Playing animation '{animationName}' for phrase: {matchedPhrase}");

        }
    }
}