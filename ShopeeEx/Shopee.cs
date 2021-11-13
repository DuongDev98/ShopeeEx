using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ShopeeEx
{
    class Shopee
    {
        string host = false ? "https://partner.shopeemobile.com/" : "https://partner.test-stable.shopeemobile.com";
        private string accessToken = "";
        public string partner_id;
        public string partner_key;
        public string shop_id;
        public string urlRedirect;

        public Shopee(string partner_id, string partner_key, string shop_id = "", string accessToken = "", string urlRedirect = "")
        {
            this.partner_id = partner_id;
            this.partner_key = partner_key;
            this.accessToken = accessToken;
            this.urlRedirect = urlRedirect;
            this.shop_id = shop_id;
        }

        //order info
        public string GetOrderDetail(string oerder_sn, ref string error)
        {
            string path = @"/api/v2/order/get_order_detail";
            Dictionary<string, object> param = GetDicShopIdAndAccessToken(true, true, false);
            param.Add("order_sn_list", oerder_sn);
            param.Add("response_optional_fields", @"buyer_user_id,buyer_username,estimated_shipping_fee,recipient_address,actual_shipping_fee ,goods_to_declare,note,note_update_time,item_list,pay_time,dropshipper,credit_card_number ,dropshipper_phone,split_up,buyer_cancel_reason,cancel_by,cancel_reason,actual_shipping_fee_confirmed,buyer_cpf_id,fulfillment_flag,pickup_done_time,package_list,shipping_carrier,payment_method,total_amount,buyer_username,invoice_data");
            return ShopeeGetRquest(path, param, "order_list", true, ref error);
        }

        //lay danh sách đơn hàng
        public string GetOrderList(DateTime fromDate, string cursor, ref string error)
        {
            DateTime toDate = DateTime.Now;

            int timestampFfromDate = (int)(fromDate.ToUniversalTime() - new DateTime(1970, 1, 1)).TotalSeconds;
            int timestampToDate = (int)(toDate.ToUniversalTime() - new DateTime(1970, 1, 1)).TotalSeconds;

            string path = @"/api/v2/order/get_order_list";
            Dictionary<string, object> param = GetDicShopIdAndAccessToken(true, true, false);
            param.Add("time_range_field", "create_time");
            param.Add("time_from", timestampFfromDate);
            param.Add("time_to", timestampToDate);
            param.Add("page_size", 3);
            param.Add("cursor", cursor);
            return ShopeeGetRquest(path, param, "response", true, ref error);
        }

        //Upload mặt hàng
        public void InsertAndUpdateProduct(string DMATHANGID, JObject item, ref string shopee_id, ref string error)
        {
            shopee_id = "";
            string path = "/api/v2/product/add_item";
            if (DMATHANGID.Length == 0)
            {
                path = "/api/v2/product/update_item";
            }
            string dataJson = JsonConvert.SerializeObject(item);
            dataJson = PostDataToShopee(path, dataJson, ref error);
            if (dataJson.Length > 0)
            {
                JObject success = JObject.Parse(dataJson);
                shopee_id = Convert.ToString(JObject.Parse(Convert.ToString(success["response"]))["item_id"]);
                if (DMATHANGID.Length > 0)
                {
                    //Config.Db.ExecSql("UPDATE DMATHANG SET SHOPEEID = '" + shopee_id + "' WHERE ID = '" + DMATHANGID + "'");
                }
                else
                {
                    //cập nhật thông tin khác thành công thì cập nhật giá, tồn kho
                    UpdatePrice(Convert.ToInt32(item["item_id"]), Convert.ToDecimal(item["original_price"]), ref error);
                    UpdateStock(Convert.ToInt32(item["item_id"]), Convert.ToDecimal(item["normal_stock"]), ref error);
                }
            }
        }

        private void UpdatePrice(int item_id, decimal price, ref string error)
        {
            string path = "/api/v2/product/update_price";
            string data = "{\"item_id\": " + item_id + ",\"price_list\": [{\"original_price\": " + price + "}]}";
            PostDataToShopee(path, data, ref error);
        }

        private void UpdateStock(int item_id, decimal stock, ref string error)
        {
            string path = "/api/v2/product/update_stock";
            string data = "{\"item_id\": " + item_id + ",\"stock_list\": [{\"normal_stock\": " + stock + "}]}";
            PostDataToShopee(path, data, ref error);
        }

        //delete mặt hàng
        public void DeleteProduct(string shopee_id, ref string error)
        {
            string path = "/api/v2/product/delete_item";
            string dataJson = "{\"item_id\": " + shopee_id + "}";
            PostDataToShopee(path, dataJson, ref error);
        }

        private string PostDataToShopee(string path, string dataJson, ref string error)
        {
            try
            {
                int timestamp = 0;
                string sign = GetSign(path, ref timestamp, true);
                string url = host + path + "?partner_id=" + partner_id + "&timestamp=" + timestamp + "&access_token=" + accessToken;
                url += "&shop_id=" + shop_id + "&sign=" + sign;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "application/json;";
                byte[] dataArr = Encoding.UTF8.GetBytes(dataJson);
                request.ContentLength = dataArr.Length;

                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(dataArr, 0, dataArr.Length);
                }

                using (StreamReader reader = new StreamReader(request.GetResponse().GetResponseStream()))
                {
                    dataJson = reader.ReadToEnd();
                    JObject res = JObject.Parse(dataJson);
                    if (Convert.ToString(res["error"]).Length > 0)
                    {
                        error = "<br/>" + Convert.ToString(res["message"]);
                        return "";
                    }
                    else
                    {
                        return dataJson;
                    }
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return "";
            }
        }

        //Upload image to shopee
        public JArray UploadFileByUrlToShopee(List<string> imgs)
        {
            JArray lst = new JArray();
            string error = "";
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls;
            //upload image, nếu có nhiều ảnh thì upload nhiều lần
            List<byte[]> lstbyteArr = new List<byte[]>();
            WebClient wc = new WebClient();
            foreach (string line in imgs)
            {
                try
                {
                    byte[] arr = wc.DownloadData(line);
                    lstbyteArr.Add(arr);
                }
                catch (Exception ex)
                {
                    if (error.Length > 0) error += Environment.NewLine;
                    error += ex.Message;
                }
            }

            if (error.Length == 0)
            {
                foreach (byte[] arr in lstbyteArr)
                {
                    //upload to shopee
                    JObject img = UploadFileToShopee(arr);
                    lst.Add(img);
                }
            }
            else
            {
                JObject img = new JObject();
                img["success"] = false;
                img["message"] = error;
                lst.Add(img);
            }

            return lst;
        }

        private JObject UploadFileToShopee(byte[] file)
        {
            string path = @"/api/v2/media_space/upload_image";
            int timestamp = 0;
            string sign = GetSign(path, ref timestamp, false);
            string url = host + path + "?partner_id=" + partner_id + "&sign=" + sign + "&timestamp=" + timestamp;
            var formContent = new MultipartFormDataContent
                {
                    {new StreamContent(new MemoryStream(file)),"image", Guid.NewGuid() + ".jpg"}
                };

            var myHttpClient = new HttpClient();
            HttpResponseMessage response = myHttpClient.PostAsync(url, formContent).Result;
            string stringContent = response.Content.ReadAsStringAsync().Result;
            JObject temp = JObject.Parse(stringContent);
            JObject val = new JObject();
            if (Convert.ToString(temp["error"]) == "")
            {
                temp = JObject.Parse(temp["response"].ToString());
                val["success"] = true;
                val["message"] = "";
                val["image_id"] = JObject.Parse(temp["image_info"].ToString())["image_id"];
            }
            else
            {
                val["success"] = false;
                val["message"] = Convert.ToString(temp["message"]);
            }

            return val;
        }

        public string GetLogisticsChannel(ref string error)
        {
            string path = @"/api/v2/logistics/get_channel_list";
            Dictionary<string, object> param = GetDicShopIdAndAccessToken();
            return ShopeeGetRquest(path, param, "logistics_channel_list", true, ref error);
        }

        public string GetCategoryAttribute(string categoryId, ref string error)
        {
            string path = @"/api/v2/product/get_attributes";
            Dictionary<string, object> param = GetDicShopIdAndAccessToken();
            param.Add("category_id", categoryId);
            return ShopeeGetRquest(path, param, "attribute_list", true, ref error);
        }

        public string GetCategoryTree(ref string error)
        {
            string path = @"/api/v2/product/get_category";
            Dictionary<string, object> param = GetDicShopIdAndAccessToken();
            return ShopeeGetRquest(path, param, "category_list", true, ref error);
        }

        private Dictionary<string, object> GetDicShopIdAndAccessToken(bool useShopId = true, bool useAccessToken = true, bool useLanguage = true)
        {
            Dictionary<string, object> dic = new Dictionary<string, object>();
            if (useShopId)
            {
                dic.Add("shop_id", shop_id);
            }

            if (useAccessToken)
            {
                dic.Add("access_token", accessToken);
            }

            if (useLanguage)
            {
                dic.Add("language", "vi");
            }

            return dic;
        }

        private string ShopeeGetRquest(string path, Dictionary<string, object> dic, string fieldData, bool usingAccessToken, ref string error)
        {
            try
            {
                int timestamp = 0;
                string sign = GetSign(path, ref timestamp, usingAccessToken);
                string url = host + path + "?partner_id=" + partner_id + "&sign=" + sign + "&timestamp=" + timestamp;

                if (dic != null && dic.Count > 0)
                {
                    foreach (string key in dic.Keys)
                    {
                        url += "&" + key + "=" + dic[key];
                    }
                }

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                using (StreamReader reader = new StreamReader(req.GetResponse().GetResponseStream()))
                {
                    string temp = reader.ReadToEnd();
                    JObject dataJson = JObject.Parse(temp);
                    if (dataJson["error"].ToString().Length == 0)
                    {
                        if (fieldData == "response")
                        {
                            return dataJson["response"].ToString();
                        }
                        else
                            return JObject.Parse(dataJson["response"].ToString())[fieldData].ToString();
                    }
                    else
                    {
                        error = dataJson["message"].ToString();
                        return "";
                    }
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return "";
            }
        }

        public string GetAccessToken(string code, ref string error)
        {
            try
            {
                string path = @"/api/v2/auth/token/get";
                int timestamp = 0;
                string sign = GetSign(path, ref timestamp, false);
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(host + path + "?partner_id=" + partner_id + "&sign=" + sign + "&timestamp=" + timestamp);
                req.Method = "POST";
                string data = "{\"partner_id\":" + partner_id + ", \"code\":\"" + code + "\", \"shop_id\":" + shop_id + "}";

                using (StreamWriter writer = new StreamWriter(req.GetRequestStream()))
                {
                    writer.Write(data);
                }

                using (StreamReader reader = new StreamReader(req.GetResponse().GetResponseStream()))
                {
                    data = reader.ReadToEnd();
                    JObject item = JObject.Parse(data);
                    if (item["message"] != null && item["message"].ToString().Length > 0)
                    {
                        error = item["message"].ToString();
                        return "";
                    }
                    else
                    {
                        return item["access_token"].ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return "";
            }
        }

        private string GetSign(string path, ref int timestamp, bool usingAccessToken)
        {
            timestamp = (int)(DateTime.Now.ToUniversalTime() - new DateTime(1970, 1, 1)).TotalSeconds;
            HMACSHA256 myhmacsha256 = new HMACSHA256(Encoding.ASCII.GetBytes(partner_key));
            string val = partner_id + path + timestamp;
            if (usingAccessToken)
            {
                val = partner_id + path + timestamp + accessToken + shop_id;
            }
            return byteToHex(myhmacsha256.ComputeHash(Encoding.ASCII.GetBytes(val)));
        }

        private static string byteToHex(byte[] byteArray)
        {
            StringBuilder result = new StringBuilder();
            foreach (byte b in byteArray)
            {
                result.AppendFormat("{0:x2}", b);
            }
            return result.ToString();
        }

        public string GetUrlCode()
        {
            string path = "/api/v2/shop/auth_partner";
            int timestamp = 0;
            string sign = GetSign(path, ref timestamp, false);
            return host + path + "?partner_id=" + partner_id + "&redirect=" + urlRedirect + "&timestamp=" + timestamp.ToString() + "&sign=" + sign;
        }

        public class Order
        {
            public int actual_shipping_fee { set; get; }
            public bool actual_shipping_fee_confirmed { set; get; }
            public string buyer_cancel_reason { set; get; }
            //public int buyer_cpf_id { set; get; }
            public int buyer_user_id { set; get; }
            public string buyer_username { set; get; }
            public string cancel_by { set; get; }
            public string cancel_reason { set; get; }
            public bool cod { set; get; }
            public long create_time { set; get; }
            public string credit_card_number { set; get; }
            public string currency { set; get; }
            public int days_to_ship { set; get; }
            public string dropshipper { set; get; }
            public string dropshipper_phone { set; get; }
            public int estimated_shipping_fee { set; get; }
            //fulfillment_flag
            public bool goods_to_declare { set; get; }
            public List<OrderItem> item_list { set; get; }
            public string message_to_seller { set; get; }
            public string note { set; get; }
            public int note_update_time { set; get; }
            public string order_sn { set; get; }
            public string order_status { set; get; }
            public int pay_time { set; get; }
            public string payment_method { set; get; }
            public int pickup_done_time { set; get; }
            public RecipientAddress recipient_address { set; get; }
            public string region { set; get; }
            public int ship_by_date { set; get; }
            public string shipping_carrier { set; get; }
            public bool split_up { set; get; }
            public int total_amount { set; get; }
            public int update_time { set; get; }
        }

        public class OrderItem
        {
            public int item_id { set; get; }
            public string item_name { set; get; }
            public string item_sku { set; get; }
            public int model_id { set; get; }
            public string model_name { set; get; }
            public string model_sku { set; get; }
            public decimal model_quantity_purchased { set; get; }
            public decimal model_original_price { set; get; }
            public decimal model_discounted_price { set; get; }
            public bool wholesale { set; get; }
            public decimal weight { set; get; }
            public bool add_on_deal { set; get; }
            public bool main_item { set; get; }
            public int add_on_deal_id { set; get; }
            public string promotion_type { set; get; }
            public int promotion_id { set; get; }
            public int order_item_id { set; get; }
            public int promotion_group_id { set; get; }
        }

        public class RecipientAddress
        {
            public string name { set; get; }
            public string phone { set; get; }
            public string town { set; get; }
            public string district { set; get; }
            public string city { set; get; }
            public string state { set; get; }
            public string region { set; get; }
            public string zipcode { set; get; }
            public string full_address { set; get; }
        }
    }
}
