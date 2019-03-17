using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;

public delegate void RecordPlayerCB(string playerID, string charityName, string msgText);
public delegate void RecordEarningCB(double amountRecorded);
public delegate void TotalResultsCB(QueryTotalResults results);
public delegate void CleanupCB();

public class QueryTotalParams {
    public string devID;
    public string gameID;
    public string playerID;
    public int previousDays;
    public int previousWeeks;
    public int previousMonths;

    public QueryTotalParams(string devID = null, string gameID = null, string playerID = null) {
        this.devID = devID;
        this.gameID = gameID;
        this.playerID = playerID;
        this.previousDays = 0;
        this.previousWeeks = 0;
        this.previousMonths = 0;
    }
}

[System.Serializable]
public class QueryTotalResults {
    public double lifetime;
    public double filtered;

    public QueryTotalResults() {
        lifetime = 0.0;
        filtered = 0.0;
    }

    public static QueryTotalResults CreateFromJSON(string jsonString)
    {
        return JsonUtility.FromJson<QueryTotalResults>(jsonString);
    }
}

[System.Serializable]
public class RecordEarningResults {
    public double amountEarned;
    public string status;

    public RecordEarningResults() {
        amountEarned = 0.0;
        status = "UNKNOWN";
    }

    public static RecordEarningResults CreateFromJSON(string jsonString)
    {
        return JsonUtility.FromJson<RecordEarningResults>(jsonString);
    }
}

public class PlayVested : MonoBehaviour {
    private const string INVALID_ID = "000000000000000000000000";

    // cached identifiers
    private string devID = INVALID_ID;
    private string gameID = INVALID_ID;
    private string playerID = INVALID_ID;
    private string charityName = "";

    // caches if player has a linked PV user account
    private bool playerIsLinked = false;

    // callbacks to notify the game when async operations are done
    private RecordPlayerCB recordPlayerCB = null;
    private CleanupCB cleanupCB = null;

    // pop ups for all the functionality
    public GameObject createPlayerObj;
    public GameObject linkAccountObj;
    public GameObject summaryObj;

    // Input fields used to link with PlayVested account
    public InputField usernameInput;
    public InputField passwordInput;

    // Totals displayed on the summary screen
    public Text lifetimeInfo;
    public Text filteredInfo;
    public Text linkErrorText;

    private string baseURL = "";

    // Use this for initialization
    void Start () {
        // determine what the base web route is
        if (Application.isEditor) {
            baseURL = "localhost:1979";
        } else {
            baseURL = "https://playvested.herokuapp.com";
        }

        // Make sure all the pop-ups are hidden when this is instantiated
        if (this.createPlayerObj) {
            this.createPlayerObj.SetActive(false);
        }
        if (this.linkAccountObj) {
            this.linkAccountObj.SetActive(false);
        }
        if (this.summaryObj) {
            this.summaryObj.SetActive(false);
        }
        if (this.usernameInput) {
            this.usernameInput.text = "";
        }
        if (this.passwordInput) {
            this.passwordInput.text = "";
        }
    }

    private IEnumerator getLinkedUser(string playerID) {
        // make the call to the web endpoint
        this.playerIsLinked = false;
        if (this.isValid(playerID)) {
            using (UnityWebRequest www = UnityWebRequest.Get(baseURL + "/players/" + playerID + "/is-linked")) {
                yield return www.SendWebRequest();

                if (www.isNetworkError || www.isHttpError) {
                    Debug.Log("Error: " + www.error);
                } else {
                    Debug.Log("Get complete! Response code: " + www.responseCode);

                    while (!(www.isDone && www.downloadHandler.isDone)) {
                        yield return new WaitForSeconds(0.1f);
                    }

                    this.playerIsLinked = (www.downloadHandler.text == "true");
                }
            }
        }
    }

    // Call this first
    // Provide the player ID if one exists
    // It will be stored on creation
    public void init(string devID, string gameID, string playerID = INVALID_ID) {
        if (this.isValid(devID)) {
            this.devID = devID;
        }
        if (this.isValid(gameID)) {
            this.gameID = gameID;
        }
        if (this.isValid(playerID)) {
            this.playerID = playerID;
        }

        StartCoroutine(this.getLinkedUser(playerID));
    }

    // Call this when ending the play session to clean up data the containing object
    public void shutdown() {
        // clean up and remove the prefab
        Destroy(this.gameObject);
    }

    // Call this to show the UI to create a new player and associate it with a charity
    public void createPlayer(RecordPlayerCB recordPlayerCB = null, CleanupCB cleanupCB = null) {
        this.recordPlayerCB = recordPlayerCB;
        this.cleanupCB = cleanupCB;
        if (this.isValid(this.gameID)) {
            this.playerID = INVALID_ID;
            this.createPlayerObj.SetActive(true);
            return;
        }

        Debug.LogError("Error: call init with game ID first");
    }

    private IEnumerator sendCreatePlayer(string charityName) {
        // make sure the game has initialized properly
        if (!this.isValid(this.gameID)) {
            Debug.Log("Error: game has not been initialized");
            yield break;
        } else if (this.isValid(this.playerID)) {
            Debug.Log("Error: player is already valid");
            yield break;
        }

        // make the call to the web endpoint
        WWWForm form = new WWWForm();
        form.AddField("charityName", charityName);
        form.AddField("gameID", this.gameID);

        using (UnityWebRequest www = UnityWebRequest.Post(baseURL + "/players", form)) {
            yield return www.SendWebRequest();

            string msgText = "";
            if (www.isNetworkError || www.isHttpError) {
                Debug.Log("Error: " + www.error);
                charityName = "";
                this.playerID = INVALID_ID;
            } else {
                Debug.Log("Form upload complete! Response code: " + www.responseCode);

                while (!(www.isDone && www.downloadHandler.isDone)) {
                    yield return new WaitForSeconds(0.1f);
                }

                Debug.Log("Body: " + www.downloadHandler.text);
                this.playerID = www.downloadHandler.text;
                msgText = "Thank you for supporting " + charityName;
            }

            // regardless of any errors, we want to close the pop up at this point
            this.handleCancelCreate(charityName, msgText);
        }
    }

    // This is hooked up to charity buttons,
    // no need to call this by hand
    public void pickCharity(string charityName) {
        Debug.Log("You clicked " + charityName);
        StartCoroutine(this.sendCreatePlayer(charityName));
    }

    // This is hooked up to the cancel button,
    // no need to call this by hand
    public void handleCancelCreate(string charityName = "", string msgText = "") {
        // we always want to store the playerID at this point
        // since even cancelling out is meaningful to the game
        if (this.recordPlayerCB != null) {
            this.recordPlayerCB(this.playerID, charityName, msgText);
        }

        this.createPlayerObj.SetActive(false);
        if (this.cleanupCB != null) {
            this.cleanupCB();
        }
    }

    private void updateTotalLabel(Text info, double value) {
        if (info != null && info.transform.parent.gameObject) {
            if (value >= 0.0) {
                info.transform.parent.gameObject.SetActive(true);
                info.text = String.Format("{0:C2}", value);
            } else {
                info.transform.parent.gameObject.SetActive(false);
            }
        } else {
            Debug.LogWarning("Couldn't find text to update, check that reference in prefab is set correctly");
        }
    }

    private IEnumerator queryTotals(QueryTotalParams queryParams, TotalResultsCB resultsCB) {
        // Clear the displayed info until we get data back from the server
        this.updateTotalLabel(this.lifetimeInfo, -1);
        this.updateTotalLabel(this.filteredInfo, -1);

        List<string> queryString = new List<string>();
        if (queryParams.gameID != null) {
            queryString.Add("gameID=" + queryParams.gameID);
            queryString.Add("devID=" + queryParams.devID);
        }
        if (queryParams.playerID != null) {
            queryString.Add("playerID=" + queryParams.playerID);
        }

        if (queryParams.previousDays != 0) {
            queryString.Add("previousDays=" + queryParams.previousDays);
        } else if (queryParams.previousWeeks != 0) {
            queryString.Add("previousWeeks=" + queryParams.previousWeeks);
        } else if (queryParams.previousMonths != 0) {
            queryString.Add("previousMonths=" + queryParams.previousMonths);
        }

        string url = baseURL + "/records/total";
        if (queryString.Count > 0) {
            url += "?";
            for (int i = 0; i < queryString.Count; i++) {
                if (i != 0) {
                    url += '&';
                }
                url += queryString[i];
            }
        }

        using (UnityWebRequest www = UnityWebRequest.Get(url)) {
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError) {
                Debug.Log("Error: " + www.error);
                this.handleCloseSummary();
            } else {
                Debug.Log("Get total complete! Response code: " + www.responseCode);

                while (!(www.isDone && www.downloadHandler.isDone)) {
                    yield return new WaitForSeconds(0.1f);
                }

                Debug.Log("Body: " + www.downloadHandler.text);
                string json = www.downloadHandler.text;
                QueryTotalResults results = QueryTotalResults.CreateFromJSON(json);

                if (resultsCB != null) {
                    resultsCB(results);
                }

                // Update the UI elements
                this.updateTotalLabel(this.lifetimeInfo, results.lifetime);
                this.updateTotalLabel(this.filteredInfo, results.filtered);
            }
        }
    }

    // Call this to show the UI to view earning details for a game
    public void showSummary(QueryTotalParams queryParams, TotalResultsCB resultsCB = null, CleanupCB cleanupCB = null) {
        this.cleanupCB = cleanupCB;

        StartCoroutine(this.queryTotals(queryParams, resultsCB));
        this.summaryObj.SetActive(true);
    }

    // Close the summary pop-up
    public void handleCloseSummary() {
        this.summaryObj.SetActive(false);
        if (this.cleanupCB != null) {
            this.cleanupCB();
        }
    }

    private IEnumerator linkAccountSuccess() {
        this.playerIsLinked = true;

        yield return new WaitForSecondsRealtime(2.0f);
        this.handleCloseLink();
    }

    private IEnumerator linkAccount(string username, string password) {
        // make the call to the web endpoint
        WWWForm form = new WWWForm();
        form.AddField("username", username);
        form.AddField("password", password);

        string linkInfo = null;
        if (this.isValid(this.playerID)) {
            linkInfo = this.playerID;
        } else if (this.isValid(this.gameID)) {
            linkInfo = "game/" + this.gameID;
        } else {
            yield break;
        }

        using (UnityWebRequest www = UnityWebRequest.Post(baseURL + "/players/link/" + linkInfo, form)) {
            yield return www.SendWebRequest();

            this.linkErrorText.text = www.downloadHandler.text;
            if (www.isNetworkError || www.isHttpError) {
                Debug.Log("Error: " + www.error);
                this.linkErrorText.color = Color.red;
            } else {
                Debug.Log("Link complete! Response code: " + www.responseCode + " body: " + www.downloadHandler.text);
                if (!this.isValid(this.playerID)) {
                    this.playerID = www.downloadHandler.text;
                    this.linkErrorText.text = "Success!";
                    string msgText = "Account successfully linked!";
                    this.handleCancelCreate("", msgText);
                }

                this.linkErrorText.color = Color.green;
                StartCoroutine(this.linkAccountSuccess());
            }
        }
    }

    // Show the link accounts pop-up
    public void handleLink() {
        StartCoroutine(this.linkAccount(this.usernameInput.text, this.passwordInput.text));
    }

    // Call this to show the UI to link with a PlayVested account
    public void handlePVLogo() {
        if (this.playerIsLinked) {
            // player is already linked, take them to the PV web page
            Application.OpenURL(baseURL + "/login");
        } else {
            // Make sure the summary object is hidden before showing the link page
            if (this.summaryObj.activeSelf) {
                this.handleCloseSummary();
            }
            if (this.linkErrorText) {
                this.linkErrorText.text = "";
            }
            this.linkAccountObj.SetActive(true);
            this.usernameInput.ActivateInputField();
        }
    }

    // Close the link accounts pop-up
    public void handleCloseLink() {
        this.usernameInput.text = "";
        this.passwordInput.text = "";
        this.linkAccountObj.SetActive(false);
    }

    public void handleCreateAccount() {
        Application.OpenURL(baseURL + "/register");
    }

    public void handleForgotPassword() {
        Application.OpenURL(baseURL + "/login");
    }

    private IEnumerator recordEarning(float amountEarned, RecordEarningCB successCB, CleanupCB cleanupCB) {
        // make the call to the web endpoint
        WWWForm form = new WWWForm();
        form.AddField("devID", this.devID);
        form.AddField("gameID", this.gameID);
        form.AddField("playerID", this.playerID);
        form.AddField("amountEarned", "" + amountEarned);

        using (UnityWebRequest www = UnityWebRequest.Post(baseURL + "/records", form)) {
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError) {
                Debug.LogError("Error: " + www.error);
            } else {
                Debug.Log("Earning report complete! Response code: " + www.responseCode);

                while (!(www.isDone && www.downloadHandler.isDone)) {
                    yield return new WaitForSeconds(0.1f);
                }

                if (successCB != null) {
                    string json = www.downloadHandler.text;
                    RecordEarningResults results = RecordEarningResults.CreateFromJSON(json);

                    successCB(results.amountEarned);
                }
            }

            if (cleanupCB != null) {
                cleanupCB();
            }
        }
    }

    public void reportEarning(float amountEarned, RecordEarningCB successCB = null, CleanupCB cleanupCB = null) {
        if (this.isValid(this.playerID)) {
            StartCoroutine(this.recordEarning(amountEarned, successCB, cleanupCB));
        } else if (cleanupCB != null) {
            cleanupCB();
        }
    }

    public bool isValid(string ID) {
        return (ID != null && ID != "" && ID != INVALID_ID);
    }
}
