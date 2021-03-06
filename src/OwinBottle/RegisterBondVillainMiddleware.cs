﻿
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FubuMVC.Core;
using FubuMVC.Core.Http.Owin;

namespace OwinBottle
{
    public class RegisterBondVillainMiddleware : IFubuRegistryExtension
    {
        public void Configure(FubuRegistry registry)
        {
            registry.AlterSettings<OwinSettings>(x => {
                x.AddMiddleware<BondVillainMiddleware>();
            });
        }
    }
}