using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Globalization;
using System.Linq;
using SimpleJSON;
using UnityEngine.SceneManagement;

public struct SettingInfo
{
    public int no;//
    public int max_limit;//
    public int open_setting_time;//
    public int sell_type;//0-ml, 1-��
    public string bus_id;
    public int decarbo_time;
    public int standby_time;
    public int sensor_spec;
    public int tagGW_no;
    public int tagGW_channel;
    public int board_no;
    public int board_channel;
}

public struct ProductInfo
{
    public string beer_id;
    public int server_id;
    public int total_amount;
    public int quantity;
    public bool is_soldout;
    public int cup_unit_price;
    public int ml_unit_price;
}

public struct TagInfo
{
    public int use_amt;
    public int prepay_amt;
    public int is_pay_after;
    public int remain;
}

enum SceneStep
{
    splash = 1,
    db_input,
    setting,
    work
}

enum WorkSceneType
{
    standby = 1,
    pour,
    soldout,
    remain
}

public class Global
{
    //image download path
    public static string imgPath = "";
    public static string prePath = "";
    public static string sdate = "";
    public static int newStatusBarValue;
    public static SettingInfo setInfo = new SettingInfo();
    public static ProductInfo beerInfo = new ProductInfo();
    public static TagInfo taginfo = new TagInfo();

    //api
    public static string pos_server_address = "";
    public static string api_server_port = "3006";
    public static string api_url = "";
    public static string check_db_api = "check-db";
    public static string get_product_api = "get-product";
    public static string cancel_soldout_api = "cancel-soldout";
    public static string soldout_api = "soldout";
    public static string keg_init_confirm_api = "keg-init-confirm";
    public static string save_setinfo_api = "save-tap-info";
    public static string image_server_path = "http://" + pos_server_address + ":" + api_server_port + "/self/";
    public static string socket_server = "";

    public static string GetPriceFormat(float price)
    {
        return string.Format("{0:N0}", price);
    }

    public static void setStatusBarValue(int value)
    {
        newStatusBarValue = value;
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                try
                {
                    activity.Call("runOnUiThread", new AndroidJavaRunnable(setStatusBarValueInThread));
                }
                catch (Exception ex)
                {
                    Debug.Log(ex);
                }
            }
        }
    }

    private static void setStatusBarValueInThread()
    {
#if UNITY_ANDROID
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                using (var window = activity.Call<AndroidJavaObject>("getWindow"))
                {
                    window.Call("setFlags", newStatusBarValue, -1);
                }
            }
        }
#endif
    }
}