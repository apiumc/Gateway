using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UMC.Data;
using UMC.Data.Entities;
using UMC.Data.Sql;
using UMC.Net;

namespace UMC.Proxy.Entities
{
    [Web.Mapping]
    public class Initializer : UMC.Data.Sql.Initializer
    {

        public override string Name => "Proxy";

        public override string Caption => "应用网关";

        public override void Setup(CSV.Log log)
        {
            Data.DataFactory.Instance().Put(new Menu()
            {
                Icon = "\uf085",
                Caption = "应用管理",
                IsHidden = false,
                ParentId = 0,
                Seq = 10,
                Id = 200,
                Url = "#proxy"

            });
        }
    }
}
