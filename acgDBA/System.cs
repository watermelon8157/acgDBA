using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace acgDBA
{
    /// <summary> 回傳值 </summary>
    public class RESPONSE_MSG
    {

        /// <summary> 處理狀態 </summary>
        public RESPONSE_STATUS status { set; get; }

        /// <summary> 傳回訊息或內容 </summary>
        public string message { set; get; }

        /// <summary> 附帶物件 </summary>
        public object attachment { set; get; }

        /// <summary> 取得序列化結果 </summary>
        public string get_json()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }

        public void setErrorMsg(string msgStr)
        {
            this.status = RESPONSE_STATUS.ERROR;
            this.message = msgStr;
        }

        public void setRESPONSE_MSG(RESPONSE_MSG rm)
        {
            this.status = rm.status;
            this.message = rm.message;
            this.attachment = rm.attachment;
        }
    }

    public enum RESPONSE_STATUS
    {
        SUCCESS = 0,
        ERROR = 1,
        EXCEPTION = 2,
        DUPLICATE = 3,
        /// <summary>
        /// 提醒 , 注意
        /// </summary>
        WARN = 4
    }
}
