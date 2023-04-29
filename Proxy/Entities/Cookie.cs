using System;
using System.Collections.Generic;
using System.Text;
using UMC.Data;

namespace UMC.Proxy.Entities
{
    public enum AccountModel
    {
        /// <summary>
        /// 标准账户
        /// </summary>
        Standard = 0,
        /// <summary>
        /// 需要托管密码
        /// </summary>
        Changed = 1,
        /// <summary>
        /// 由Check账户而来
        /// </summary>
        Check = 2
    }

    public partial class Cookie : Record
    {
        /// <summary>
        /// 站点
        /// </summary> 
        public string Domain
        {
            get; set;
        }
        /// <summary>
        /// 服务密码
        /// </summary>
        public Guid? user_id
        {
            get; set;
        }
        public string Cookies
        {
            get; set;
        }
        /// <summary>
        /// 个数
        /// </summary>
        public int? IndexValue { get; set; }
        /// <summary>
        /// 账户
        /// </summary>
        public string Account
        {
            get; set;
        }

        public DateTime? Time
        {
            get; set;
        }
        /// <summary>
        /// 更新密码时间
        /// </summary>
        public int? ChangedTime
        {
            get; set;
        }
        /// <summary>
        /// 最近登录时间
        /// </summary>
        public int? LoginTime
        {
            get; set;
        }
        public int? Badge { get; set; }

        public String Config { get; set; }


        public AccountModel? Model { get; set; }
         
       

    }
}
