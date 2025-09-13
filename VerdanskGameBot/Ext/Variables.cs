using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace VerdanskGameBot.Ext
{
    public class GamedigGamesJsonDocument(JsonDocument jsondoc)
    {
        public JsonElement RootElement => jsondoc.RootElement;
    }

    public class SteamRedirectPage
    {
        public Uri Url { get; private set; }

        public SteamRedirectPage(Uri url)
        {
            if (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)
                throw new ArgumentException("URL must be HTTP or HTTPS", nameof(url));

            Url = url;
        }
    }
}
