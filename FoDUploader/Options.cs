﻿
using CommandLine;
using CommandLine.Text;

namespace FoDUploader
{
    class Options
    {
        public string GetUsage()
        {
            var help = new HelpText
            {
                Heading = new HeadingInfo("FoD Uploader", "1.0"),
                Copyright = new CopyrightInfo("HPE Fortify on Demand, Ryan Black", 2016),
                AdditionalNewLineAfterOption = true,
                AddDashesToOption = true
            };

            help.AddPreOptionsLine("");
            help.AddPostOptionsLine("Usage: foduploader.exe --optionName \"Value\"");
            help.AddOptions(this);

            return help;

        }

        [Option("source", Required = true, HelpText = "The ZIP file, or directory to be zipped, for submission to Fortify on Demand")]
        public string source { get; set; }

        [Option("username", HelpText = "Your portal username.", MutuallyExclusiveSet = "userauth")]
        public string username { get; set; }

        [Option("password", HelpText = "Your portal password.", MutuallyExclusiveSet = "userauth")]
        public string password { get; set; }

        [Option("apiToken", HelpText = "Your api token.", MutuallyExclusiveSet = "tokenauth")]
        public string apiToken { get; set; }

        [Option("apiTokenSecret", HelpText = "Your api token secret key.", MutuallyExclusiveSet = "tokenauth")]
        public string apiTokenSecret { get; set; }

        [Option("uploadURL", Required = true, HelpText = "Your BSI URL for the target application, obtained in the customer portal.")]
        public string uploadURL { get; set; }

        [Option("sonatypeReport", DefaultValue = false, HelpText = "If set to true, and enabled in the tenant, a Sonatype report will be requested.")]
        public bool sonatypeReport { get; set; }

        [Option("automatedAudit", DefaultValue = false, HelpText = "If set to true, and enabled in the tenant, the Automated Audit feature will be requested.")]
        public bool automatedAudit { get; set; }

        [Option("expressScan", DefaultValue = false, HelpText = "If set to true, and enabled in the tenant, the Express Scan feature will be requested.")]
        public bool expressScan { get; set; }

        [Option("proxyURI", HelpText = "Your proxy URI.")]
        public string proxyURI { get; set; }

        [Option("proxyUsername", HelpText = "Your proxy username.")]
        public string proxyUsername { get; set; }

        [Option("proxyPassword", HelpText = "Your proxy password.")]
        public string proxyPassword { get; set; }

        [Option("ntDomain", HelpText = "Your NT domain.")]
        public string ntDomain { get; set; }

        [Option("ntWorkstation", HelpText = "Your NT workstation.")]
        public string ntWorkstation { get; set; }

    }
}