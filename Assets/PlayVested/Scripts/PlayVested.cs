using UnityEngine.Networking;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using UnityEngine;

public delegate void RecordUser(string userID);
public delegate void RecordEarning(bool success);
public delegate void TotalResults(QueryTotalResults results);
public delegate void FailureCleanup();

public class QueryTotalParams {
    public string userID;
    public string gameID;
    public int previousDays;
    public int previousWeeks;
    public int previousMonths;

    public QueryTotalParams() {
        userID = null;
        gameID = null;
        previousDays = 0;
        previousWeeks = 0;
        previousMonths = 0;
    }
}

public class QueryTotalResults {
    public double lifetime;
    public double filtered;

    public QueryTotalResults() {
        lifetime = 0.0;
        filtered = 0.0;
    }
}

public class PlayVested : MonoBehaviour {
    // cached identifiers for the game and user
    private string gameID = "";
    private string userID = "";

    // callbacks to notify the game when async operations are done
    private RecordUser recordUserCB = null;
    private RecordEarning recordEarningCB = null;
    private FailureCleanup failureCB = null;

    // pop ups for all the functionality
    public GameObject createUserObj;
    public GameObject linkAccountObj;
    public GameObject summaryObj;

    // Input fields used to link with PlayVested account
    public InputField usernameInput;
    public InputField passwordInput;

    // Totals displayed on the summary screen
    public GameObject lifetimeInfo;
    public GameObject filteredInfo;

    //*
    private string baseURL = "localhost:1979";
    /*/
    private string baseURL = "https://playvested.herokuapp.com";
    */

    // Use this for initialization
    void Start () {
        // Make sure all the pop-ups are hidden when this is instantiated
        if (this.createUserObj) {
            this.createUserObj.SetActive(false);
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

    // Call this first
    // Provide the user ID if one exists
    // It will be stored on creation
    public void init(string gameID, string userID = "") {
        Debug.Log("Init called with: " + gameID + " : " + userID);
        this.gameID = gameID;
        this.userID = userID;
    }

    // Call this when ending the play session to clean up data the containing object
    public void shutdown() {
        // clean up and remove the prefab
        Destroy(this.gameObject);
    }

    // Call this to show the UI to create a new user and associate it with a charity
    public void createUser(RecordUser recordUserCB = null, FailureCleanup failureCB = null) {
        this.recordUserCB = recordUserCB;
        this.failureCB = failureCB;
        if (this.gameID != "") {
            this.createUserObj.SetActive(true);
            return;
        }

        Debug.LogError("Error: call init with game ID first");
    }

    private IEnumerator sendRequest(string charityID) {
        // make sure the game has initialized properly
        if (this.gameID == "") {
            Debug.Log("Error: game has not been initialized");
            yield break;
        } else if (this.userID != "") {
            Debug.Log("Error: user is already valid");
            yield break;
        }

        // make the call to the web endpoint
        WWWForm form = new WWWForm();
        // form.AddField("username", "ImaTest");
        // form.AddField("email", "ima@test.com");
        // form.AddField("firstName", "Ima");
        // form.AddField("lastName", "Test");
        form.AddField("charityID", charityID);
        form.AddField("gameID", this.gameID);

        using (UnityWebRequest www = UnityWebRequest.Post(baseURL + "/users", form)) {
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError) {
                Debug.Log("Error: " + www.error);
                if (this.failureCB != null) {
                    this.failureCB();
                }
            } else {
                Debug.Log("Form upload complete! Response code: " + www.responseCode);

                while (!(www.isDone && www.downloadHandler.isDone)) {
                    yield return new WaitForSeconds(0.1f);
                }

                Debug.Log("Body: " + www.downloadHandler.text);
                this.userID = www.downloadHandler.text;

                if (this.recordUserCB != null) {
                    this.recordUserCB(this.userID);
                }
            }

            // regardless of any errors, we want to close the pop up at this point
            this.handleCancelCreate();
        }
    }

    // This is hooked up to charity buttons,
    // no need to call this by hand
    public void handleClick(string charityID) {
        Debug.Log("You clicked " + charityID);
        StartCoroutine(this.sendRequest(charityID));
    }

    // This is hooked up to the cancel button,
    // no need to call this by hand
    public void handleCancelCreate() {
        this.createUserObj.SetActive(false);
    }

    private void updateTotalLabel(GameObject info, double value) {
        if (info != null) {
            Text label = info.GetComponent<Text>();
            if (label != null) {
                if (value > 0.0) {
                    info.SetActive(true);
                    label.text = "$" + value;
                    return;
                }
            }
        }

        info.SetActive(false);
    }

    private IEnumerator queryTotals(QueryTotalParams queryParams, TotalResults resultsCB) {
        // Clear the displayed info until we get data back from the server
        this.updateTotalLabel(this.lifetimeInfo, 0);
        this.updateTotalLabel(this.filteredInfo, 0);

        List<string> queryString = new List<string>();
        if (queryParams.gameID != null) {
            queryString.Add("gameID=" + queryParams.gameID);
        }

        if (queryParams.userID != null) {
            queryString.Add("userID=" + queryParams.userID);
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
                    url += ',';
                }
                url += queryString[i];
            }
        }

        using (UnityWebRequest www = UnityWebRequest.Get(url)) {
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError) {
                Debug.Log("Error: " + www.error);
                if (this.failureCB != null) {
                    this.failureCB();
                }
            } else {
                Debug.Log("Get total complete! Response code: " + www.responseCode);

                while (!(www.isDone && www.downloadHandler.isDone)) {
                    yield return new WaitForSeconds(0.1f);
                }

                Debug.Log("Body: " + www.downloadHandler.text);
                QueryTotalResults results = new QueryTotalResults();
                results.lifetime = System.Convert.ToDouble(www.downloadHandler.text);
                results.filtered = System.Convert.ToDouble(www.downloadHandler.text);

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
    public void showSummary(QueryTotalParams queryParams, TotalResults resultsCB = null) {
        if (this.gameID != "") {
            this.summaryObj.SetActive(true);
            return;
        }

        StartCoroutine(this.queryTotals(queryParams, resultsCB));

        Debug.LogError("Error: call init with game ID first");
    }

    // Close the summary pop-up
    public void handleCloseSummary() {
        this.summaryObj.SetActive(false);
    }

    private IEnumerator linkAccount(string username, string password) {
        // make the call to the web endpoint
        WWWForm form = new WWWForm();
        form.AddField("username", username);
        form.AddField("password", password);

        using (UnityWebRequest www = UnityWebRequest.Post(baseURL + "/users/link", form)) {
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError) {
                Debug.Log("Error: " + www.error);
            } else {
                Debug.Log("Link complete! Response code: " + www.responseCode + " body: " + www.downloadHandler.text);
            }

            // regardless of any errors, we want to close the pop up at this point
            this.handleCloseLink();
        }
    }

    // Show the link accounts pop-up
    public void handleLink() {
        StartCoroutine(this.linkAccount(this.usernameInput.text, this.passwordInput.text));
    }

    // Call this to show the UI to link with a PlayVested account
    public void showLinkAccount() {
        // Make sure the summary object is hidden before showing the link page
        this.handleCloseSummary();
        this.linkAccountObj.SetActive(true);
    }

    // Close the link accounts pop-up
    public void handleCloseLink() {
        this.usernameInput.text = "";
        this.passwordInput.text = "";
        this.linkAccountObj.SetActive(false);
    }

    private IEnumerator recordEarning(float amountEarned) {
        // make the call to the web endpoint
        WWWForm form = new WWWForm();
        form.AddField("gameID", this.gameID);
        form.AddField("userID", this.userID);
        form.AddField("amountEarned", "" + amountEarned);

        using (UnityWebRequest www = UnityWebRequest.Post(baseURL + "/records", form)) {
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError) {
                Debug.LogError("Error: " + www.error);
                if (this.failureCB != null) {
                    this.failureCB();
                }
            } else {
                Debug.Log("Earning report complete! Response code: " + www.responseCode);

                bool retVal = (www.isDone && www.downloadHandler.isDone);
                if (this.recordEarningCB != null) {
                    this.recordEarningCB(retVal);
                }
                yield return retVal;
            }
        }
    }

    public void reportEarning(float amountEarned, RecordEarning recordEarningCB = null, FailureCleanup failureCB = null) {
        this.recordEarningCB = recordEarningCB;
        this.failureCB = failureCB;

        if (this.gameID == "" || this.userID == "") {
            Debug.LogError("Error: game and user have not been initialized");
            return;
        }

        StartCoroutine(this.recordEarning(amountEarned));
    }
}
