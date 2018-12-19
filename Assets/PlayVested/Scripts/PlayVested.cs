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

public class PlayVested : MonoBehaviour {
    // cached identifiers for the game and user
    private string gameID = "";
    private string userID = "";

    // callbacks to notify the game when async operations are done
    private RecordUser recordUserCB = null;
    private RecordEarning recordEarningCB = null;

    // pop ups for all the functionality
    public GameObject createUserObj;
    public GameObject linkAccountObj;
    public GameObject summaryObj;

    // Input fields used to link with PlayVested account
    public InputField usernameInput;
    public InputField passwordInput;

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
    public void createUser(RecordUser recordUserCB = null) {
        this.recordUserCB = recordUserCB;
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
            } else {
                Debug.Log("Form upload complete! Response code: " + www.responseCode);

                if (www.isDone && www.downloadHandler.isDone) {
                    Debug.Log("Body: " + www.downloadHandler.text);
                    this.userID = www.downloadHandler.text;

                    if (this.recordUserCB != null) {
                        this.recordUserCB(this.userID);
                    }
                } else {
                    Debug.Log("Not done downloading yet");
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

    // Call this to show the UI to view earning details for a game
    public void showSummary() {
        if (this.gameID != "") {
            this.summaryObj.SetActive(true);
            return;
        }

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

    public void reportEarning(float amountEarned, RecordEarning recordEarningCB = null) {
        this.recordEarningCB = recordEarningCB;

        if (this.gameID == "" || this.userID == "") {
            Debug.LogError("Error: game and user have not been initialized");
            return;
        }

        StartCoroutine(this.recordEarning(amountEarned));
    }
}
