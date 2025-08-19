using UnityEngine;
using Microsoft.CognitiveServices.Speech;
using TMPro;
using UnityEngine.UI;

public class AzureVoicecRecognizer : MonoBehaviour
{
    [SerializeField] private string subscriptionKey, region;
    private object threadLocker = new object();
    private bool waitingForReco;
    private string message;

    private bool micPermissionGranted = false;

    [SerializeField] private Button startRecordButton;
    [SerializeField] private TextMeshProUGUI outputMessageText;

    public async void ButtonClick()
    {

        var config = SpeechConfig.FromSubscription(subscriptionKey,region);

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
        }
    }
}
