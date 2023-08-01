using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SimpleJSON;
using SocketIO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;
using System.Threading;

public class MainManager : MonoBehaviour
{
    bool is_from_splash = false;
    WorkSceneType workscenetype;
    //splash
    public GameObject splashObj;
    public GameObject shop_open_popup;
    public GameObject decarbonate_popup;
    public GameObject splash_err_popup;
    public Text splash_err_title;
    public Text splash_err_content;
    public float delay_time = 0.5f;
    public float repeat_time = 5f;
    bool is_connection = false;

    //work
    public GameObject standbyImgObj;
    public GameObject pourImgObj;
    public GameObject remainImgObj;
    public GameObject workObj;
    public GameObject soldoutObj;
    public GameObject priceObj;
    public Text contentObj;
    public Text noticeObj;
    public GameObject socketPrefab;
    GameObject socketObj;
    SocketIOComponent socket;
    public GameObject err_popup;
    public Text err_title;
    public Text err_content;

    //setting
    public GameObject settingObj;
    public GameObject washPopup;
    public GameObject kegPopup;
    public GameObject devicecheckingPopup;
    public GameObject kegInitPopup;
    public GameObject set_savePopup;
    public GameObject set_errPopup;
    public Text set_errStr;
    public GameObject dbinputObj;

    public float response_delay_time = 5f;
    bool is_self = false;
    bool is_last = false;
    bool shopFlag = false;
    bool standBTFlag = false;
    bool shopCloseHandType = false;

    public int flag = 0;
    long prev_flowmeter_value = 0;

    //setting ui
    public InputField no;
    //db input ui
    public InputField[] dbInput = new InputField[2];//0-posip, 1-busniness number
    public AudioSource[] soundObjs; //touch:0, alarm_soldout:1, alarm_max:2, alarm_remain:3, alarm_standby:4, alarm_beerchange:5 ,alarm_shopopen:6, alarm_shopclose:7, wash-8, keginit-9, alarm_start:10, alarm_prepay:11

    void Awake()
    {
        Screen.orientation = ScreenOrientation.Portrait;
        Screen.fullScreen = true;
#if UNITY_ANDROID
        Global.setStatusBarValue(1024); // WindowManager.LayoutParams.FLAG_FORCE_NOT_FULLSCREEN
#endif
        soundObjs[10].Play();
    }

    // Start is called before the first frame update
    IEnumerator Start()
    {
#if UNITY_IPHONE
		Global.imgPath = Application.persistentDataPath + "/bself_beer_img/";
#elif UNITY_ANDROID
        Global.imgPath = Application.persistentDataPath + "/bself_beer_img/";
#else
if( Application.isEditor == true ){ 
    	Global.imgPath = "/img/";
} 
#endif

#if UNITY_IPHONE
		Global.prePath = @"file://";
#elif UNITY_ANDROID
        Global.prePath = @"file:///";
#else
		Global.prePath = @"file://" + Application.dataPath.Replace("/Assets","/");
#endif

        //delete all downloaded images
        try
        {
            if (Directory.Exists(Global.imgPath))
            {
                Directory.Delete(Global.imgPath, true);
            }
            LoadInfoFromPrefab();
        }
        catch (Exception)
        {

        }

        if (Global.pos_server_address == "" || Global.setInfo.bus_id == "")
        {
            Debug.Log("server = " + Global.pos_server_address);
            yield return new WaitForSeconds(delay_time);
            onShowScene(SceneStep.db_input);
        }
        else if (Global.setInfo.no == 0)
        {
            yield return new WaitForSeconds(delay_time);
            onShowScene(SceneStep.setting);
        }
        else
        {
            StartCoroutine(Connect());
        }
    }

    void LoadInfoFromPrefab()
    {
        Global.setInfo.no = PlayerPrefs.GetInt("no");
        Global.setInfo.bus_id = PlayerPrefs.GetString("bus_id");
        Global.pos_server_address = PlayerPrefs.GetString("ip");
        Global.socket_server = "ws://" + Global.pos_server_address + ":" + Global.api_server_port;
        Global.api_url = "http://" + Global.pos_server_address + ":" + Global.api_server_port + "/m-api/self/";
        Global.image_server_path = "http://" + Global.pos_server_address + ":" + Global.api_server_port + "/self/";
    }

    bool is_send_check_connect = false;
    IEnumerator Connect()
    {
        while (true)
        {
            if (is_connection)
                break;
            if (!is_send_check_connect)
            {
                WWWForm form = new WWWForm();
                form.AddField("bus_id", Global.setInfo.bus_id);
                form.AddField("serial_number", Global.setInfo.no);
                Debug.Log("autosave-----------------------------");
                WWW www = new WWW(Global.api_url + Global.check_db_api, form);
                StartCoroutine(ProcessCheckConnect(www));
                is_send_check_connect = true;
            }
            yield return new WaitForSeconds(3f);
        }
    }

    IEnumerator ProcessCheckConnect(WWW www)
    {
        yield return www;
        if (www.error == null)
        {
            JSONNode jsonNode = SimpleJSON.JSON.Parse(www.text);
            string result = jsonNode["suc"].ToString()/*.Replace("\"", "")*/;
            if (result == "1")
            {
                try
                {
                    if (jsonNode["is_self"].AsInt == 1)
                    {
                        is_self = true;
                    }
                    else
                    {
                        is_self = false;
                    }
                } catch(Exception ex)
                {
                    is_self = false;
                }
                Global.setInfo.max_limit = jsonNode["max"].AsInt;
                Global.setInfo.open_setting_time = jsonNode["opentime"].AsInt;
                Global.setInfo.sell_type = jsonNode["sell_type"].AsInt;
                Global.setInfo.decarbo_time = jsonNode["decarbo_time"].AsInt;
                Global.setInfo.standby_time = jsonNode["standby_time"].AsInt;
                Global.setInfo.sensor_spec = jsonNode["sensor"].AsInt;
                StopCoroutine("Connect");
                yield return new WaitForSeconds(delay_time);
                splash_err_popup.SetActive(false);
                StopCoroutine("checkIsSelf");
                StartCoroutine(checkIsSelf());
                is_connection = true;
                if (socketObj == null && socket == null)
                {
                    InitSocketFunctions();
                }
                DownloadWorkImg(true);
                StartCoroutine(ProcessGetProductInfo());
                onShowScene(SceneStep.work);
                workscenetype = WorkSceneType.standby;
                LoadInfo();
                Debug.Log("check connect");
            }
            else
            {
                splash_err_title.text = "Connecting to Server";
                splash_err_content.text = "서버와 연결 중입니다. 잠시만 기다려주세요.";
                splash_err_popup.SetActive(true);
            }
        }
        else
        {
            is_send_check_connect = false;
            splash_err_title.text = "Connecting to Server";
            splash_err_content.text = "서버와 연결 중입니다. 잠시만 기다려주세요.";
            splash_err_popup.SetActive(true);
        }
    }

    IEnumerator checkIsSelf()
    {
        while (true)
        {
            yield return new WaitForSeconds(6 * 3600);
            WWWForm form = new WWWForm();
            form.AddField("bus_id", Global.setInfo.bus_id);
            WWW www = new WWW(Global.api_url + Global.check_db_api, form);
            StartCoroutine(ProcessCheckIsSelf(www));
        }
    }

    IEnumerator ProcessCheckIsSelf(WWW www)
    {
        yield return www;
        if (www.error == null)
        {
            JSONNode jsonNode = SimpleJSON.JSON.Parse(www.text);
            string result = jsonNode["suc"].ToString()/*.Replace("\"", "")*/;
            if (result == "1")
            {
                try
                {
                    if (jsonNode["is_self"].AsInt == 1)
                    {
                        is_self = true;
                    }
                    else
                    {
                        is_self = false;
                    }
                } catch(Exception ex)
                {
                    is_self = false;
                }
            }
            else
            {
                is_self = false;
            }
        }
        else
        {
            is_self = false;
        }
    }

    void onShowScene(SceneStep scene_step)
    {
        if(scene_step != SceneStep.setting)
        {
            splashObj.SetActive(false);
            workObj.SetActive(false);
            settingObj.SetActive(false);
            dbinputObj.SetActive(false);
        }
        switch (scene_step)
        {
            case SceneStep.splash://splash
                {
                    splashObj.SetActive(true);
                    dbinputObj.SetActive(false);
                    settingObj.SetActive(false);
                    workObj.SetActive(false);
                    break;
                };
            case SceneStep.db_input://db input
                {
                    dbinputObj.SetActive(true);
                    splashObj.SetActive(false);
                    settingObj.SetActive(false);
                    workObj.SetActive(false);
                    try
                    {
                        dbInput[0].text = Global.pos_server_address;
                        dbInput[1].text = Global.setInfo.bus_id;
                    }
                    catch (Exception ex)
                    {
                        Debug.Log(ex);
                    }
                    break;
                };
            case SceneStep.setting://setting
                {
                    splashObj.SetActive(false);
                    workObj.SetActive(false);
                    dbinputObj.SetActive(false);
                    settingObj.SetActive(true);
                    try
                    {
                        no.text = Global.setInfo.no.ToString();
                    }
                    catch (Exception ex)
                    {
                        Debug.Log(ex);
                    }
                    break;
                };
            case SceneStep.work://work
                {
                    workObj.SetActive(true);
                    splashObj.SetActive(false);
                    dbinputObj.SetActive(false);
                    settingObj.SetActive(false);
                    break;
                };
        }
    }

    IEnumerator ProcessGetProductInfo()
    {
        while (true)
        {
            WWWForm form = new WWWForm();
            form.AddField("serial_number", Global.setInfo.no);
            WWW www = new WWW(Global.api_url + Global.get_product_api, form);
            yield return www;
            if (www.error == null)
            {
                JSONNode jsonNode = SimpleJSON.JSON.Parse(www.text);
                string result = jsonNode["suc"].ToString()/*.Replace("\"", "")*/;
                if (result == "1")
                {
                    flag++;
                    Global.beerInfo.beer_id = jsonNode["beer_id"];
                    if (Global.beerInfo.beer_id == "" || Global.beerInfo.beer_id == null)
                    {
                        priceObj.SetActive(false);
                        DownloadWorkImg(false);
                        yield return new WaitForSeconds(repeat_time);
                    }
                    else
                    {
                        Global.beerInfo.server_id = jsonNode["server_id"].AsInt;
                        Global.beerInfo.total_amount = jsonNode["total_amount"].AsInt;
                        Global.beerInfo.quantity = jsonNode["quantity"].AsInt;
                        Global.beerInfo.cup_unit_price = jsonNode["cup_unit_price"].AsInt;
                        Global.beerInfo.ml_unit_price = jsonNode["unit_price"].AsInt;

                        if (Global.beerInfo.server_id != PlayerPrefs.GetInt("beer_server_id") || flag == 1)
                        {
                            Debug.Log("DownLoadWorkImag");
                            DownloadWorkImg(false);
                        }
                        PlayerPrefs.SetInt("beer_server_id", Global.beerInfo.server_id);
                        if (jsonNode["sold_out"].AsInt == 1)
                        {
                            Global.beerInfo.is_soldout = true;
                        }
                        else
                        {
                            Global.beerInfo.is_soldout = false;
                        }
                        if (Global.beerInfo.is_soldout == true)
                            workscenetype = WorkSceneType.soldout;
                        LoadInfo();
                        break;
                    }
                }
                else
                {
                    err_title.text = "No Beer";
                    err_content.text = "맥주정보를 확인하세요.";
                    err_popup.SetActive(true);
                    yield return new WaitForSeconds(repeat_time);
                }
            }
            else
            {
                err_title.text = "No Beer";
                err_content.text = "맥주정보를 확인하세요.";
                err_popup.SetActive(true);
                yield return new WaitForSeconds(repeat_time);
            }
        }
    }

    public void onDBInput()
    {
        onShowScene(SceneStep.db_input);
    }

    public void onBack()
    {
        if (dbinputObj.activeSelf)
        {
            onShowScene(SceneStep.setting);
        }
        else if (settingObj.activeSelf)
        {
            if (Global.setInfo.no == 0)
            {

                set_errStr.text = "설정값들을 저장하세요.";
                set_errPopup.SetActive(true);
            }
            else
            {
                string tagGWData = "{\"tagGW_no\":\"" + Global.setInfo.tagGW_no + "\"," +
                    "\"ch_value\":\"" + Global.setInfo.tagGW_channel + "\"," +
                    "\"status\":\"" + 1 + "\"}";
                socket.Emit("deviceTagLock", JSONObject.Create(tagGWData));
                onShowScene(SceneStep.work);
                //StartCoroutine(ProcessGetProductInfo());
                StartCoroutine(Connect());
            }
        }
        else
        {
            string tagGWData = "{\"tagGW_no\":\"" + Global.setInfo.tagGW_no + "\"," +
                "\"ch_value\":\"" + Global.setInfo.tagGW_channel + "\"," +
                "\"status\":\"" + 1 + "\"}";
            socket.Emit("deviceTagLock", JSONObject.Create(tagGWData));
            onShowScene(SceneStep.work);
        }
    }

    public void SaveSetInfo()
    {
        if (no.text == "")
        {
            set_errStr.text = "기기번호를 입력하세요.";
            set_errPopup.SetActive(true);
        }
        else
        {
            WWWForm form = new WWWForm();
            form.AddField("serial_number", int.Parse(no.text));
            form.AddField("total_amount", Global.beerInfo.total_amount);
            WWW www = new WWW(Global.api_url + Global.save_setinfo_api, form);
            StartCoroutine(saveSetProcess(www));
        }
    }

    IEnumerator saveSetProcess(WWW www)
    {
        yield return www;
        if (www.error == null)
        {
            JSONNode jsonNode = SimpleJSON.JSON.Parse(www.text);
            Global.setInfo.no = int.Parse(no.text);
            PlayerPrefs.SetInt("no", Global.setInfo.no);
            Global.setInfo.board_channel = jsonNode["board_channel"].AsInt;
            Global.setInfo.board_no = jsonNode["board_no"].AsInt;
            Global.setInfo.tagGW_channel = jsonNode["tagGW_channel"].AsInt;
            Global.setInfo.tagGW_no = jsonNode["tagGW_no"].AsInt;
            StartCoroutine(ProcessGetProductInfo());
            set_savePopup.SetActive(true);
        }
        else
        {
            set_errStr.text = "저장에 실패하였습니다.";
            set_errPopup.SetActive(true);
        }
    }

    public void onConfirmErrPopup()
    {
        set_errStr.text = "";
        set_errPopup.SetActive(false);
    }

    public void onConfirmSavePopup()
    {
        set_savePopup.SetActive(false);
    }

    public void Wash()
    {
        if (Global.beerInfo.beer_id == "" || Global.beerInfo.beer_id == null)
        {
            set_errStr.text = "맥주정보가 없습니다.";
            set_errPopup.SetActive(true);
        }
        else
        {
            soundObjs[8].Play();
            washPopup.SetActive(true);
            if(socket != null)
            {
                string data = "{\"board_no\":\"" + Global.setInfo.board_no + "\"," +
                    "\"ch_value\":\"" + Global.setInfo.board_channel + "\"," +
                    "\"valve\":\"" + 0 + "\"," +
                    "\"status\":\"" + 1 + "\"}";
                socket.Emit("boardValveCtrl", JSONObject.Create(data));
            }
        }
    }

    public void onConfirmWashPopup()
    {
        Debug.Log("Finish washing.");
        washPopup.SetActive(false);
        workscenetype = WorkSceneType.standby;
        onShowScene(SceneStep.work);
        LoadInfo();
        if(socket != null)
        {
            string tagGWData = "{\"tagGW_no\":\"" + Global.setInfo.tagGW_no + "\"," +
                "\"ch_value\":\"" + Global.setInfo.tagGW_channel + "\"," +
                "\"status\":\"" + 1 + "\"}";
            socket.Emit("deviceTagLock", JSONObject.Create(tagGWData));

            string data = "{\"board_no\":\"" + Global.setInfo.board_no + "\"," +
                "\"ch_value\":\"" + Global.setInfo.board_channel + "\"," +
                "\"valve\":\"" + 0 + "\"," +
                "\"status\":\"" + 0 + "\"}";
            socket.Emit("boardValveCtrl", JSONObject.Create(data));
        }
    }

    public void KegChange()
    {
        if (Global.beerInfo.beer_id == "" || Global.beerInfo.beer_id == null)
        {
            set_errStr.text = "맥주정보가 없습니다.";
            set_errPopup.SetActive(true);
        }
        else
        {
            kegPopup.SetActive(true);
            if(socket != null)
            {
                string tagGWData = "{\"tagGW_no\":\"" + Global.setInfo.tagGW_no + "\"," +
                "\"ch_value\":\"" + Global.setInfo.tagGW_channel + "\"," +
                "\"status\":\"" + 0 + "\"}";
                socket.Emit("deviceTagLock", JSONObject.Create(tagGWData));

                string data = "{\"board_no\":\"" + Global.setInfo.board_no + "\"," +
                    "\"ch_value\":\"" + Global.setInfo.board_channel + "\"," +
                    "\"valve\":\"" + 0 + "\"," +
                    "\"status\":\"" + 1 + "\"}";
                socket.Emit("boardValveCtrl", JSONObject.Create(data));
            }
        }
    }

    public void onConfirmKegPopup()
    {
        kegPopup.SetActive(false);
        kegInitPopup.SetActive(true);
    }

    IEnumerator ProcessKegInitConfirmApi()
    {
        WWWForm form = new WWWForm();
        form.AddField("serial_number", Global.setInfo.no);
        form.AddField("total_amount", Global.beerInfo.total_amount);
        WWW www = new WWW(Global.api_url + Global.keg_init_confirm_api, form);
        yield return www;
        if (www.error == null)
        {
            JSONNode jsonNode = SimpleJSON.JSON.Parse(www.text);
            string result = jsonNode["suc"].ToString()/*.Replace("\"", "")*/;
            if (result == "1")
            {
                soundObjs[9].Play();
                kegInitPopup.SetActive(false);
                err_popup.SetActive(false);
                onShowScene(SceneStep.work);
                workscenetype = WorkSceneType.standby;
                LoadInfo();
                if (socket != null)
                {
                    string data = "{\"board_no\":\"" + Global.setInfo.board_no + "\"," +
                        "\"ch_value\":\"" + Global.setInfo.board_channel + "\"," +
                        "\"valve\":\"" + 0 + "\"," +
                        "\"status\":\"" + 0 + "\"}";
                    socket.Emit("boardValveCtrl", JSONObject.Create(data));

                    data = "{\"tagGW_no\":\"" + Global.setInfo.tagGW_no + "\"," +
                        "\"ch_value\":\"" + Global.setInfo.tagGW_channel + "\"," +
                        "\"status\":\"" + 1 + "\"}";
                    socket.Emit("deviceTagLock", JSONObject.Create(data));
                }
            }
        }
        else
        {
            err_title.text = "Connecting to Server";
            err_content.text = "서버와 연결 중입니다. 잠시만 기다려주세요.";
            err_popup.SetActive(true);
        }
    }

    public void onConfirmkegInitPopup()
    {
        if(socket != null)
        {
            string data = "{\"board_no\":\"" + Global.setInfo.board_no + "\"," +
                "\"ch_value\":\"" + Global.setInfo.board_channel + "\"," +
                "\"valve\":\"" + 0 + "\"," +
                "\"status\":\"" + 1 + "\"}";
            socket.Emit("boardValveCtrl", JSONObject.Create(data));
            StartCoroutine(ProcessKegInitConfirmApi());
        }
    }

    public void onCancelKegInitPopup()
    {
        if(socket != null)
        {
            string data = "{\"board_no\":\"" + Global.setInfo.board_no + "\"," +
                "\"ch_value\":\"" + Global.setInfo.board_channel + "\"," +
                "\"valve\":\"" + 0 + "\"," +
                "\"status\":\"" + 0 + "\"}";
            socket.Emit("boardValveCtrl", JSONObject.Create(data));
        }
        kegInitPopup.SetActive(false);
        onShowScene(SceneStep.setting);
    }

    public void Soldout()
    {
        if (Global.beerInfo.beer_id == "" || Global.beerInfo.beer_id == null)
        {
            set_errStr.text = "맥주정보가 없습니다.";
            set_errPopup.SetActive(true);
        }
        else
        {
            WWWForm form = new WWWForm();
            form.AddField("serial_number", Global.setInfo.no);
            WWW www = new WWW(Global.api_url + Global.soldout_api, form);
            StartCoroutine(Soldout(www));
        }
    }

    public void DBInfoSave()
    {
        if (dbInput[0].text == "")
        {
            set_errStr.text = "IP를 입력하세요.";
            set_errPopup.SetActive(true);
        }
		else if(dbInput[1].text == "")
        {
            set_errStr.text = "사업자번호를 입력하세요.";
            set_errPopup.SetActive(true);
        }
        else
        {
            Global.pos_server_address = dbInput[0].text;
            PlayerPrefs.SetString("ip", Global.pos_server_address);
            Global.socket_server = "ws://" + Global.pos_server_address + ":" + Global.api_server_port;
            Global.api_url = "http://" + Global.pos_server_address + ":" + Global.api_server_port + "/m-api/self/";
            Global.image_server_path = "http://" + Global.pos_server_address + ":" + Global.api_server_port + "/self/";
            WWWForm form = new WWWForm();
            form.AddField("bus_id", dbInput[1].text);
            Debug.Log("save----------------------------------------------------------");
            WWW www = new WWW(Global.api_url + Global.check_db_api, form);
            StartCoroutine(ProcessCheckConnect1(www));
        }
    }

    void InitSocketFunctions()
    {
        socketObj = Instantiate(socketPrefab);
        socket = socketObj.GetComponent<SocketIOComponent>();
        socket.On("open", socketOpen);
        socket.On("LoadDeivceInfo", LoadDeviceInfo);
        socket.On("shopOpen", OpenShopEventHandler);
        socket.On("shopClose", CloseShopEventHandler);
        socket.On("soldoutOccured", SoldoutEventHandler);
        socket.On("RepairingDevice", RepairingDevice);
        socket.On("changeProductInfo", ChangeBeerInfo);
        socket.On("changeSetInfo", ChangeSetInfo);
        socket.On("selftagVerifyResponse", TagVerifyResponse);
        //socket.On("startResponse", startResponse);
        socket.On("flowmeterValue", FlowmeterValueEventHandler);
        socket.On("flowmeterFinish", FlowmeterFinishEventHandler);
        socket.On("ConnectFailInfo", ConnectFailInfo);
        socket.On("FinishFailInfo", ConnectFailInfo);
        //socket.On("boardconnectionFailed", boardconnectionFailed);
        //socket.On("gwconnectionFailed", boardconnectionFailed);
        socket.On("error", socketError);
        socket.On("close", socketClose);
    }

    IEnumerator ProcessCheckConnect1(WWW www)
    {
        yield return www;
        if (www.error == null)
        {
            JSONNode jsonNode = SimpleJSON.JSON.Parse(www.text);
            string result = jsonNode["suc"].ToString()/*.Replace("\"", "")*/;
            if (result == "1")
            {
                try
                {
                    if (jsonNode["is_self"].AsInt == 1)
                    {
                        is_self = true;
                    }
                    else
                    {
                        is_self = false;
                    }
                } catch(Exception e)
                {
                    is_self = false;
                }
                Global.pos_server_address = dbInput[0].text;
                PlayerPrefs.SetString("ip", Global.pos_server_address);
                Global.socket_server = "ws://" + Global.pos_server_address + ":" + Global.api_server_port;
                Global.api_url = "http://" + Global.pos_server_address + ":" + Global.api_server_port + "/m-api/self/";
                Global.image_server_path = "http://" + Global.pos_server_address + ":" + Global.api_server_port + "/self/";
                Global.setInfo.bus_id = dbInput[1].text;
                PlayerPrefs.SetString("bus_id", Global.setInfo.bus_id);
                if (socket != null)
                {
                    socket.Close();
                    socket.OnDestroy();
                    socket.OnApplicationQuit();
                }
                if (socketObj != null)
                {
                    DestroyImmediate(socketObj);
                }
                yield return new WaitForSeconds(0.5f);
                InitSocketFunctions();
                set_savePopup.SetActive(true);
                StopCoroutine("checkIsSelf");
                StartCoroutine(checkIsSelf());
            }
            else
            {
                set_errStr.text = "디비정보를 확인하세요.";
                set_errPopup.SetActive(true);
            }
        }
        else
        {
            set_errStr.text = "디비정보를 확인하세요.";
            set_errPopup.SetActive(true);
        }
    }

    public void onDecarbonate()
    {
        decarbonate_popup.SetActive(true);
        StartCoroutine(Decarbonate());
    }

    IEnumerator Decarbonate()
    {
        if(socket != null)
        {
            string data = "{\"board_no\":\"" + Global.setInfo.board_no + "\"," +
                "\"ch_value\":\"" + Global.setInfo.board_channel + "\"," +
                "\"valve\":\"" + 1 + "\"," +
                "\"status\":\"" + 1 + "\"}";
            socket.Emit("boardValveCtrl", JSONObject.Create(data));
        }
        yield return new WaitForSeconds(Global.setInfo.decarbo_time);
        if (!shopCloseHandType && socket != null)
        {
            string data = "{\"tagGW_no\":\"" + Global.setInfo.tagGW_no + "\"," +
                "\"ch_value\":\"" + Global.setInfo.tagGW_channel + "\"," +
                "\"status\":\"" + 1 + "\"}";
            socket.Emit("deviceTagLock", JSONObject.Create(data));

            data = "{\"board_no\":\"" + Global.setInfo.board_no + "\"," +
                "\"ch_value\":\"" + Global.setInfo.board_channel + "\"," +
                "\"valve\":\"" + 1 + "\"," +
                "\"status\":\"" + 0 + "\"}";
            socket.Emit("boardValveCtrl", JSONObject.Create(data));

            decarbonate_popup.SetActive(false);
            onShowScene(SceneStep.work);
            workscenetype = WorkSceneType.standby;
            LoadInfo();
        }
    }

    IEnumerator shopDecarbonate()
    {
        Debug.Log("stop decarbonate from shop close event.");
        yield return new WaitForSeconds(Global.setInfo.decarbo_time);
        if (!shopCloseHandType && socket != null)
        {
            string data1 = "{\"board_no\":\"" + Global.setInfo.board_no + "\"," +
                "\"ch_value\":\"" + Global.setInfo.board_channel + "\"," +
                "\"valve\":\"" + 1 + "\"," +
                "\"status\":\"" + 0 + "\"}";
            socket.Emit("boardValveCtrl", JSONObject.Create(data1));
            decarbonate_popup.SetActive(false);
        }
        onShowScene(SceneStep.work);
        workscenetype = WorkSceneType.standby;
        LoadInfo();
    }

    void DownloadWorkImg(bool is_init)
    {
        if (is_init)
        {
            remainImgObj.SetActive(true);
            string ImgUrl = Global.image_server_path + "Remain.jpg";
            StartCoroutine(downloadImage(ImgUrl, Global.imgPath + Path.GetFileName(ImgUrl), remainImgObj));
        }
        else
        {
            standbyImgObj.SetActive(true);
            pourImgObj.SetActive(true);

            if (Global.beerInfo.beer_id == "" || Global.beerInfo.beer_id == null)
            {
                pourImgObj.GetComponent<Image>().sprite = Resources.Load<Sprite>("noBeer");
                standbyImgObj.GetComponent<Image>().sprite = Resources.Load<Sprite>("noBeer");
            }
            else
            {
                string downloadImgUrl = Global.image_server_path + "Pour" + Global.beerInfo.server_id + ".jpg";
                StartCoroutine(downloadImage(downloadImgUrl, Global.imgPath + Path.GetFileName(downloadImgUrl), pourImgObj));
                downloadImgUrl = Global.image_server_path + "Standby" + Global.beerInfo.server_id + ".jpg";
                StartCoroutine(downloadImage(downloadImgUrl, Global.imgPath + Path.GetFileName(downloadImgUrl), standbyImgObj));
            }
        }
        LoadWorkImg(workscenetype);
    }

    void LoadWorkImg(WorkSceneType scene_type)
    {
        switch (scene_type)
        {
            case WorkSceneType.standby:
                {
                    standbyImgObj.SetActive(true);
                    pourImgObj.SetActive(false);
                    remainImgObj.SetActive(false);
                    break;
                };
            case WorkSceneType.pour:
                {
                    standbyImgObj.SetActive(false);
                    pourImgObj.SetActive(true);
                    remainImgObj.SetActive(false);
                    break;
                };
            case WorkSceneType.remain:
                {
                    standbyImgObj.SetActive(false);
                    pourImgObj.SetActive(false);
                    remainImgObj.SetActive(true);
                    break;
                };
            case WorkSceneType.soldout:
                {
                    standbyImgObj.SetActive(true);
                    pourImgObj.SetActive(false);
                    remainImgObj.SetActive(false);
                    break;
                };
        }
    }

    void LoadInfo()
    {
        if (!is_self)
        {
            workscenetype = WorkSceneType.standby;
        }
        LoadWorkImg(workscenetype);
        switch (workscenetype)
        {
            case WorkSceneType.standby:
                {
                    //standby
                    if (Global.setInfo.sell_type == 0)
                    {
                        contentObj.text = Global.GetPriceFormat(Global.beerInfo.ml_unit_price);
                        noticeObj.text = "원/ml";
                    }
                    else
                    {
                        contentObj.text = Global.GetPriceFormat(Global.beerInfo.cup_unit_price);
                        noticeObj.text = "원/잔";
                    }
                    priceObj.SetActive(true);
                    soldoutObj.SetActive(false);
                    break;
                };
            case WorkSceneType.pour:
                {
                    //pour
                    noticeObj.text = "ml";
                    //if (Global.setInfo.sell_type == 1 && is_last)
                    //{
                    //    contentObj.text = Global.GetPriceFormat(Global.beerInfo.quantity);
                    //}
                    //else
                    //{
                        contentObj.text = Global.GetPriceFormat(Global.beerInfo.quantity);
                    //}
                    priceObj.SetActive(true);
                    soldoutObj.SetActive(false);
                    break;
                };
            case WorkSceneType.soldout:
                {
                    //soldout
                    try
                    {
                        soldoutObj.SetActive(true);
                        priceObj.SetActive(false);
                    }
                    catch (Exception err)
                    {
                        Debug.Log(err);
                    }
                    break;
                };
            case WorkSceneType.remain:
                {
                    //remain
                    soundObjs[3].Play();
                    noticeObj.text = "원";
                    contentObj.text = Global.GetPriceFormat(Global.taginfo.remain);
                    priceObj.SetActive(true);
                    soldoutObj.SetActive(false);
                    break;
                }
        }
    }

    public void socketOpen(SocketIOEvent e)
    {
        if(is_socket_open)
        {
            return;
        }
        if(Global.setInfo.no != 0 && socket != null)
        {
            is_socket_open = true;
            string no = "{\"no\":\"" + Global.setInfo.no + "\"}";
            Debug.Log(no);
            socket.Emit("selfSetInfo", JSONObject.Create(no));
            Debug.Log("[SocketIO] Open received: " + e.name + " " + e.data);
        }
    }

    public void LoadDeviceInfo(SocketIOEvent e)
    {
        JSONNode jsonNode = SimpleJSON.JSON.Parse(e.data.ToString());
        Debug.Log("load deviceInfo = " + jsonNode);
        try
        {
            Global.setInfo.board_channel = jsonNode["board_channel"].AsInt;
            Global.setInfo.board_no = jsonNode["board_no"].AsInt;
            Global.setInfo.tagGW_channel = jsonNode["taggw_channel"].AsInt;
            Global.setInfo.tagGW_no = jsonNode["taggw_no"].AsInt;
        }catch(Exception ex)
        {
            Debug.Log(ex);
        }
    }

    public void ChangeBeerInfo(SocketIOEvent e)
    {
        soundObjs[5].Play();
        JSONNode jsonNode = SimpleJSON.JSON.Parse(e.data.ToString());
        Debug.Log(jsonNode);
        int s_number = jsonNode["serial_number"].AsInt;
        if (s_number != Global.setInfo.no)
        {
            return;
        }
        Global.beerInfo.server_id = jsonNode["product_id"].AsInt;
        string standby_url = Global.imgPath + "Standby" + Global.beerInfo.server_id + ".jpg";
        string pous_url = Global.imgPath + "Pour" + Global.beerInfo.server_id + ".jpg";
        string soldout_url = Global.imgPath + "Soldout" + Global.beerInfo.server_id + ".jpg";
        if (File.Exists(standby_url))
        {
            File.Delete(standby_url);
        }
        if (File.Exists(pous_url))
        {
            File.Delete(pous_url);
        }
        if (File.Exists(soldout_url))
        {
            File.Delete(soldout_url);
        }
        StartCoroutine(ProcessGetProductInfo());
        LoadInfo();
    }

    public void ChangeSetInfo(SocketIOEvent e)
    {
        JSONNode jsonNode = SimpleJSON.JSON.Parse(e.data.ToString());
        Debug.Log(jsonNode);
        int s_number = jsonNode["serial_number"].AsInt;
        if (s_number != Global.setInfo.no)
            return;
        Global.setInfo.max_limit = jsonNode["max"].AsInt;
        Global.setInfo.open_setting_time = jsonNode["opentime"].AsInt;
        Global.setInfo.sell_type = jsonNode["sell_type"].AsInt;
        Global.setInfo.decarbo_time = jsonNode["decarbo_time"].AsInt;
        Global.setInfo.standby_time = jsonNode["standby_time"].AsInt;
        Global.setInfo.sensor_spec = jsonNode["sensor"].AsInt;
    }

    DateTime open_tagResponse_time = new DateTime();
    DateTime open_tagResponse_timeA = new DateTime();

    //public void startResponse(SocketIOEvent e)
    //{
    //    try
    //    {
    //        open_tagResponse_time = DateTime.Now;
    //        open_tagResponse_timeA = open_tagResponse_time.AddMinutes(Global.setInfo.standby_time);
    //        onShowScene(SceneStep.work);
    //        workscenetype = WorkSceneType.pour;
    //        LoadInfo();
    //    }
    //    catch (Exception ex)
    //    {
    //        Debug.Log(ex);
    //    }
    //}

    public void TagVerifyResponse(SocketIOEvent e)
    {
        try
        {
            JSONNode jsonNode = SimpleJSON.JSON.Parse(e.data.ToString());
            Debug.Log(jsonNode);
            if (Global.beerInfo.beer_id == "" || Global.beerInfo.beer_id == null)
            {
                return;
            }
            if(jsonNode["is_manage_card"].AsInt == 1)
            {
                string tagGWData = "{\"tagGW_no\":\"" + Global.setInfo.tagGW_no + "\"," +
                    "\"ch_value\":\"" + Global.setInfo.tagGW_channel + "\"," +
                    "\"status\":\"" + 0 + "\"}";
                socket.Emit("deviceTagLock", JSONObject.Create(tagGWData));
                onShowScene(SceneStep.setting);
            }
            else
            {
                onShowScene(SceneStep.work);
                if (workscenetype != WorkSceneType.soldout && is_self)
                {
                    if (jsonNode["suc"].AsInt == 1)
                    {
                        int status = jsonNode["status"].AsInt;
                        switch (status)
                        {
                            case 0:
                                {
                                    err_title.text = "Invalid Tag";
                                    err_content.text = "사용할 수 없는 태그입니다.";
                                    StartCoroutine(ShowErrPopup());
                                    break;
                                };
                            case 1:
                                {
                                    workscenetype = WorkSceneType.pour;
                                    LoadInfo();
                                    Debug.Log("TagVerifyResponse--");
                                    JSONNode tagData = JSON.Parse(jsonNode["tag"].ToString()/*.Replace("\"", "")*/);
                                    Global.taginfo = new TagInfo();
                                    Global.taginfo.use_amt = tagData["use_amt"].AsInt;
                                    Global.taginfo.prepay_amt = tagData["prepay_amt"].AsInt;
                                    Global.taginfo.remain = tagData["remain"].AsInt;
                                    Global.taginfo.is_pay_after = tagData["is_pay_after"].AsInt;
                                    is_last = false;
                                    break;
                                };
                            case 2:
                                {
                                    err_title.text = "Unregistered Tag";
                                    err_content.text = "등록되지 않은 태그입니다.";
                                    StartCoroutine(ShowErrPopup());
                                    break;
                                };
                            case 3:
                                {
                                    err_title.text = "Lost Tag";
                                    err_content.text = "분실된 TAG입니다.\n카운터에 반납해주세요.";
                                    StartCoroutine(ShowErrPopup());
                                    break;
                                };
                            case 4:
                                {
                                    err_title.text = "Recharge your Tag";
                                    err_content.text = "남은 금액이 없습니다.\n충전 후에 사용하세요.";
                                    StartCoroutine(ShowErrPopup());
                                    break;
                                };
                            case 5:
                                {
                                    err_title.text = "Recharge your Tag";
                                    err_content.text = "남은 금액이 부족합니다.\n충전 후에 사용하세요.";
                                    StartCoroutine(ShowErrPopup());
                                    break;
                                };
                            case 6:
                                {
                                    err_title.text = "Expired Tag";
                                    err_content.text = "사용기한이 지난 태그입니다.";
                                    StartCoroutine(ShowErrPopup());
                                    break;
                                };
                        }
                    }
                    else
                    {
                        err_title.text = "Invalid Tag";
                        err_content.text = "사용할 수 없는 태그입니다.";
                        StartCoroutine(ShowErrPopup());
                    }
                }
            }
        }
        catch (Exception err)
        {
            Debug.Log(err);
        }
    }

    public void FlowmeterValueEventHandler(SocketIOEvent e)
    {
        try
        {
            Debug.Log("Flowmeter value event.");
            JSONNode jsonNode = SimpleJSON.JSON.Parse(e.data.ToString());
            standBTFlag = true;
            long flowmeter_value = jsonNode["flowmeter_value"].AsLong;
            int volume_sec = jsonNode["volume_sec"].AsInt;
            int board_no = jsonNode["board_no"].AsInt;
            int ch_value = jsonNode["ch_value"].AsInt;
            Debug.Log("precv value = " + prev_flowmeter_value);
            Debug.Log("value = " + flowmeter_value);
            Debug.Log("volsec = " + volume_sec);
            if ((flowmeter_value - prev_flowmeter_value) > volume_sec)
            {
                string FlowStop = "{\"board_no\":\"" + board_no + "\"," +
            "\"ch_value\":\"" + ch_value + "\"}";
                socket.Emit("boardFlowStop", JSONObject.Create(FlowStop));
                Debug.Log("STOP");
            }
            prev_flowmeter_value = flowmeter_value;
            Global.beerInfo.quantity = (int)(flowmeter_value);
            workscenetype = WorkSceneType.pour;
            LoadInfo();
        }
        catch (Exception err)
        {
            Debug.Log(err);
        }
    }

    public void onConfirmDeviceCheckingPopup()
    {
        devicecheckingPopup.SetActive(false);
        string tagGWData = "{\"tagGW_no\":\"" + Global.setInfo.tagGW_no + "\"," +
            "\"ch_value\":\"" + Global.setInfo.tagGW_channel + "\"," +
            "\"status\":\"" + 1 + "\"}";
        socket.Emit("deviceTagLock", JSONObject.Create(tagGWData));
    }

    //public void boardconnectionFailed(SocketIOEvent e)
    //{
    //    Debug.Log("tcp disconnection event.");
    //    JSONNode jsonNode = SimpleJSON.JSON.Parse(e.data.ToString());
    //    int status = jsonNode["status"].AsInt;
    //    if (status == 1)
    //    {
    //        err_popup.SetActive(false);
    //    }
    //    else
    //    {
    //        err_title.text = "Connecting to Server";
    //        err_content.text = "서버와 연결 중입니다. 잠시만 기다려주세요.";
    //        err_popup.SetActive(true);
    //    }
    //}

    public void ConnectFailInfo(SocketIOEvent e)
    {
        Debug.Log("socket connect failed event.");
        err_title.text = "Connecting to Server";
        err_content.text = "서버와 연결 중입니다. 잠시만 기다려주세요.";
        err_popup.SetActive(true);
        onShowScene(SceneStep.work);
        workscenetype = WorkSceneType.standby;
        LoadInfo();
    }

    public void FlowmeterFinishEventHandler(SocketIOEvent e)
    {
        try
        {
            JSONNode jsonNode = SimpleJSON.JSON.Parse(e.data.ToString());
            Debug.Log(jsonNode);
            long flowmeter_value = jsonNode["flowmeter_value"].AsLong;
            JSONNode tagData = JSON.Parse(jsonNode["tag"].ToString()/*.Replace("\"", "")*/);
            int status = jsonNode["status"].AsInt;
            if (flowmeter_value != 0)
            {
                Global.taginfo = new TagInfo();
                Global.taginfo.remain = tagData["remain"].AsInt;
                Global.taginfo.is_pay_after = tagData["is_pay_after"].AsInt;
            }
            onShowScene(SceneStep.work);
            Global.beerInfo.quantity = (int)(flowmeter_value);
            workscenetype = WorkSceneType.pour;
            LoadInfo();
            switch (status)
            {
                case 0:
                    {
                        //정상종료
                        if (Global.taginfo.is_pay_after == 1)
                        {
                            StartCoroutine(ReturntoStandby(1f));
                        }
                        else
                        {
                            StartCoroutine(ReturnFromRemain());
                        }
                        break;
                    };
                case 1:
                    {
                        //MAX차단
                        //soundObjs[2].Play();
                        if(Global.taginfo.is_pay_after == 1)
                        {
                            StartCoroutine(ReturntoStandby(1f));
                        }
                        else
                        {
                            StartCoroutine(ReturnFromRemain());
                        }
                        break;
                    };
                case 2:
                    {
                        if(Global.taginfo.is_pay_after == 1)
                        {
                            StartCoroutine(ReturntoStandby(1f));
                        }
                        else
                        {
                            StartCoroutine(ReturnFromRemain());
                        }
                        //EMPTY로 차단
                        break;
                    };
                case 3:
                    {
                        //SOLDOUT로 차단
                        Global.beerInfo.is_soldout = true;
                        workscenetype = WorkSceneType.soldout;
                        soundObjs[1].Play();
                        LoadInfo();
                        break;
                    };
            }
            is_last = true;
            prev_flowmeter_value = 0;
            Global.beerInfo.quantity = 0;
        }
        catch (Exception err)
        {
            Debug.Log(err);
        }
    }

    IEnumerator ReturnFromRemain()
    {
        yield return new WaitForSeconds(1f);
        workscenetype = WorkSceneType.remain;
        LoadInfo();
        StartCoroutine(ReturntoStandby(3f));
    }

    DateTime open_market_time = new DateTime();
    DateTime open_market_timeA = new DateTime();

    public void OpenShopEventHandler(SocketIOEvent e)
    {
        soundObjs[6].Play();
        open_market_time = DateTime.Now;
        open_market_timeA = open_market_time.AddMinutes(Global.setInfo.open_setting_time);
        shop_open_popup.SetActive(true);
    }

    public void CloseShopEventHandler(SocketIOEvent e)
    {
        soundObjs[7].Play();
        JSONNode jsonNode = SimpleJSON.JSON.Parse(e.data.ToString());
        int res = jsonNode["res"].AsInt;
        if (res == 1)
        {
            decarbonate_popup.SetActive(true);
            StartCoroutine(shopDecarbonate());
        }
        else
        {
            onShowScene(SceneStep.work);
            workscenetype = WorkSceneType.standby;
            LoadInfo();
        }
    }

    public void RepairingDevice(SocketIOEvent e)
    {
        devicecheckingPopup.SetActive(true);
    }

    public void SoldoutEventHandler(SocketIOEvent e)
    {
        try
        {
            soundObjs[1].Play();
            Debug.Log("[SocketIO] Soldout received: " + e.name + " " + e.data);
            JSONNode jsonNode = SimpleJSON.JSON.Parse(e.data.ToString());
            int is_soldout = jsonNode["is_soldout"].AsInt;
            if (is_soldout != 1)
            {
                Global.beerInfo.is_soldout = false;
                onShowScene(SceneStep.work);
                workscenetype = WorkSceneType.standby;
            }
            else
            {
                Global.beerInfo.is_soldout = true;
                workscenetype = WorkSceneType.soldout;
            }
            LoadInfo();
        }
        catch (Exception err)
        {
            Debug.Log(err);
        }
    }

    public void socketError(SocketIOEvent e)
    {
        Debug.Log("[SocketIO] Error received: " + e.name + " " + e.data);
    }

    public void socketClose(SocketIOEvent e)
    {
        Debug.Log("[SocketIO] Close received: " + e.name + " " + e.data);
        is_socket_open = false;
    }

    IEnumerator ShowErrPopup()
    {
        err_popup.SetActive(true);
        yield return new WaitForSeconds(3f);
        err_popup.SetActive(false);
        workscenetype = WorkSceneType.standby;
        onShowScene(SceneStep.work);
        LoadInfo();
    }

    IEnumerator ReturntoStandby(float delay_time)
    {
        yield return new WaitForSeconds(delay_time);
        soundObjs[4].Play();
        workscenetype = WorkSceneType.standby;
        LoadInfo();
        string tagGWData = "{\"tagGW_no\":\"" + Global.setInfo.tagGW_no + "\"," +
            "\"ch_value\":\"" + Global.setInfo.tagGW_channel + "\"," +
            "\"status\":\"" + 1 + "\"}";
        socket.Emit("deviceTagLock", JSONObject.Create(tagGWData));
    }

    public void OpenSettingTimeOver()
    {
        if(socket != null)
        {
            string tagGWData = "{\"tagGW_no\":\"" + Global.setInfo.tagGW_no + "\"," +
                "\"ch_value\":\"" + 100 + "\"," +
                "\"status\":\"" + 1 + "\"}";
            socket.Emit("deviceTagLock", JSONObject.Create(tagGWData));

            string boardData = "{\"board_no\":\"" + Global.setInfo.board_no + "\"," +
                "\"ch_value\":\"" + 100 + "\"," +
                "\"valve\":\"" + 0 + "\"," +
                "\"status\":\"" + 0 + "\"}";
            socket.Emit("boardValveCtrl", JSONObject.Create(boardData));

            open_market_time = new DateTime();
            shopFlag = true;
            standBTFlag = true;
            shop_open_popup.SetActive(false);
        }
    }

    public void onConfirmShopOpenPopup()
    {
        shop_open_popup.SetActive(false);
        onShowScene(SceneStep.work);
        workscenetype = WorkSceneType.standby;
        LoadInfo();
        shopFlag = true;
        OpenSettingTimeOver();
    }

    public void onConfirmDecarbonatePopup()
    {
        Debug.Log("confirm decarbonation");
        decarbonate_popup.SetActive(false);
        onShowScene(SceneStep.work);
        workscenetype = WorkSceneType.standby;
        LoadInfo();
        if(socket != null)
        {
            string data = "{\"tagGW_no\":\"" + Global.setInfo.tagGW_no + "\"," +
                "\"ch_value\":\"" + Global.setInfo.tagGW_channel + "\"," +
                "\"status\":\"" + 1 + "\"}";
            socket.Emit("deviceTagLock", JSONObject.Create(data));

            data = "{\"board_no\":\"" + Global.setInfo.board_no + "\"," +
                "\"ch_value\":\"" + Global.setInfo.tagGW_channel + "\"," +
                "\"valve\":\"" + 1 + "\"," +
                "\"status\":\"" + 0 + "\"}";
            socket.Emit("boardValveCtrl", JSONObject.Create(data));
        }
        shopCloseHandType = true;
    }

    //download image
    IEnumerator downloadImage(string url, string pathToSaveImage, GameObject imgObj)
    {
        yield return new WaitForSeconds(0.001f);
        Image img = imgObj.GetComponent<Image>();
        if (File.Exists(pathToSaveImage))
        {
            Debug.Log(pathToSaveImage + " exists");
            StartCoroutine(LoadPictureToTexture(pathToSaveImage, img));
        }
        else
        {
            Debug.Log(pathToSaveImage + " downloading--");
            WWW www = new WWW(url);
            StartCoroutine(_downloadImage(www, pathToSaveImage, img));
        }
    }

    IEnumerator LoadPictureToTexture(string name, Image img)
    {
        //Debug.Log("load image = " + Global.prePath + name);
        WWW pictureWWW = new WWW(Global.prePath + name);
        yield return pictureWWW;

        try
        {
            if (img != null)
            {
                img.sprite = Sprite.Create(pictureWWW.texture, new Rect(0, 0, pictureWWW.texture.width, pictureWWW.texture.height), new Vector2(0, 0), 8f, 0, SpriteMeshType.FullRect);
            }
        }
        catch (Exception ex)
        {
            Debug.Log(ex);
        }
    }

    private IEnumerator _downloadImage(WWW www, string savePath, Image img)
    {
        yield return www;
        //Check if we failed to send
        if (string.IsNullOrEmpty(www.error))
        {
            saveImage(savePath, www.bytes, img);
        }
        else
        {
            UnityEngine.Debug.Log("Error: " + www.error);
        }
    }

    void saveImage(string path, byte[] imageBytes, Image img)
    {
        try
        {
            //Create Directory if it does not exist
            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }
            File.WriteAllBytes(path, imageBytes);
            //Debug.Log("Download Image: " + path.Replace("/", "\\"));
            StartCoroutine(LoadPictureToTexture(path, img));
        }
        catch (Exception e)
        {
            Debug.LogWarning("Failed To Save Data to: " + path.Replace("/", "\\"));
            Debug.LogWarning("Error: " + e.Message);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (open_market_time == new DateTime())
        {
            return;
        }
        if (DateTime.Now >= open_market_timeA && !shopFlag)
        {
            OpenSettingTimeOver();
        }
        if (open_tagResponse_time == new DateTime())
        {
            return;
        }
        if (DateTime.Now >= open_tagResponse_timeA && !standBTFlag)
        {
            Debug.Log("update");
            workscenetype = WorkSceneType.standby;
            onShowScene(SceneStep.work);
            LoadInfo();
        }
    }

    int order = 0;
    public void onClickOrder1()
    {
        Debug.Log("Clicklefttop");
        if (Global.beerInfo.is_soldout)
        {
            WWWForm form = new WWWForm();
            form.AddField("serial_number", Global.setInfo.no);
            WWW www = new WWW(Global.api_url + Global.cancel_soldout_api, form);
            StartCoroutine(CancelSoldout(www));
        }
        order = 1;
    }

    public void onClickOrder2()
    {
        if(order == 1)
        {
            order = 2;
        }
        else
        {
            order = 0;
        }
    }

    public void onClickOrder3()
    {
        if(order == 2)
        {
            order = 3;
        }
        else
        {
            order = 0;
        }
    }

    public void onClickOrder4()
    {
        if(order == 3)
        {
            onShowScene(SceneStep.setting);
            order = 0;
            string tagGWData = "{\"tagGW_no\":\"" + Global.setInfo.tagGW_no + "\"," +
                "\"ch_value\":\"" + Global.setInfo.tagGW_channel + "\"," +
                "\"status\":\"" + 0 + "\"}";
            socket.Emit("deviceTagLock", JSONObject.Create(tagGWData));
        }
        else
        {
            order = 0;
        }
    }

    IEnumerator CancelSoldout(WWW www)
    {
        yield return www;
        if (www.error == null)
        {
            JSONNode jsonNode = SimpleJSON.JSON.Parse(www.text);
            int result = jsonNode["suc"].AsInt;
            if (result == 1)
            {
                onShowScene(SceneStep.work);
                workscenetype = WorkSceneType.standby;
                LoadInfo();
                if(socket != null)
                {
                    string tagGWData = "{\"tagGW_no\":\"" + Global.setInfo.tagGW_no + "\"," +
                        "\"ch_value\":\"" + Global.setInfo.tagGW_channel + "\"," +
                        "\"status\":\"" + 1 + "\"}";
                    socket.Emit("deviceTagLock", JSONObject.Create(tagGWData));
                }
                err_popup.SetActive(false);
            }
            else
            {
                set_errStr.text = "서버와의 조작시 알지 못할 오류가 발생하였습니다.";
                set_errPopup.SetActive(true);
            }
        }
        else
        {
            set_errStr.text = "서버와의 조작시 알지 못할 오류가 발생하였습니다.";
            set_errPopup.SetActive(true);
        }

    }

    IEnumerator Soldout(WWW www)
    {
        yield return www;
        if (www.error == null)
        {
            JSONNode jsonNode = SimpleJSON.JSON.Parse(www.text);
            int result = jsonNode["suc"].AsInt;
            if (result == 1)
            {
                soundObjs[1].Play();
                workscenetype = WorkSceneType.soldout;
                Debug.Log("soldout");
                onShowScene(SceneStep.work);
                Global.beerInfo.is_soldout = true;
                LoadInfo();
                err_title.text = "";
                err_content.text = "";
                err_popup.SetActive(false);
            }
            else
            {
                set_errStr.text = "서버와의 조작시 알지 못할 오류가 발생하였습니다.";
                set_errPopup.SetActive(true);
            }
        }
        else
        {
            set_errStr.text = "서버와의 조작시 알지 못할 오류가 발생하였습니다.";
            set_errPopup.SetActive(true);
        }
    }

    float time = 0f;
    private bool is_socket_open = false;

    void FixedUpdate()
    {
        if (!Input.anyKey)
        {
            time += Time.deltaTime;
        }
        else
        {
            if (time != 0f)
            {
                soundObjs[0].Play();
                time = 0f;
            }
        }
    }
}
