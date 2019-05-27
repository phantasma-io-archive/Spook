using System.Collections.Generic;

namespace LunarLabs.Utils
{
    public enum Continent
    {
        Asia,    
        Oceania,   
        Africa,    
        Europe,    
        North_America,    
        South_America,
    }

    public enum Country
    {
        Unknown,
        Afghanistan,
	    Albania,
        Algeria,
        American_Samoa,
        Andorra,
        Angola,
        Anguilla,
        Antarctica,
        Antigua_and_Barbuda,
        Argentina,
        Armenia,
        Aruba,
        Australia,
        Austria,
        Azerbaijan,
        Bahamas,
        Bahrain,
        Bangladesh,
        Barbados,
        Belarus,
        Belgium,
        Belize,
        Benin,
        Bermuda,
        Bhutan,
        Bolivia,
        Bosnia_and_Herzegovina,
        Botswana,
        Bouvet_Island,
        Brazil,
        Brunei,
        Bulgaria,
        Burkina_Faso,
        Burundi,
        Cambodia,
        Cameroon,
        Canada,
        Cabo_Verde,
        Cayman_Islands,
        Central_African_Republic,
        Chad,
        Chile,
        China,
        Colombia,
        Comoros,
        Congo,
        Cook_Islands,
        Costa_Rica,
        Croatia,
        Cuba,
        Cyprus,
        Czech_Republic,
        Denmark,
        Djibouti,
        Dominica,
        Dominican_Republic,
        Ecuador,
        Egypt,
        El_Salvador,
        Equatorial_Guinea,
        Eritrea,
        Estonia,
        Ethiopia,
        Faroe_Islands,
        Fiji,
        Finland,
        France,
        Gabon,
        Gambia,
        Georgia,
        Germany,
        Ghana,
        Gibraltar,
        Greece,
        Greenland,
        Grenada,
        Guadeloupe,
        Guam,
        Guatemala,
        Guernsey,
        Guinea,
        Guinea_Bissau,
        Guyana,
        Haiti,
        Vatican,
        Honduras,
        Hong_Kong,
        Hungary,
        Iceland,
        India,
        Indonesia,
        Iran,
        Iraq,
        Ireland,
        Isle_of_Man,
        Israel,
        Italy,
        Jamaica,
        Japan,
        Jersey,
        Jordan,
        Kazakhstan,
        Kenya,
        Kiribati,
        North_Korea,
        South_Korea,
        Kuwait,
        Kyrgyzstan,
        Lao,
        Latvia,
        Lebanon,
        Lesotho,
        Liberia,
        Libya,
        Liechtenstein,
        Lithuania,
        Luxembourg,
        Macao,
        Macedonia,
        Madagascar,
        Malawi,
        Malaysia,
        Maldives,
        Mali,
        Malta,
        Marshall_Islands,
        Martinique,
        Mauritania,
        Mauritius,
        Mayotte,
        Mexico,
        Micronesia, 
        Moldova,
        Monaco,
        Mongolia,
        Montenegro,
        Montserrat,
        Morocco,
        Mozambique,
        Myanmar,
        Namibia,
        Nauru,
        Nepal,
        Netherlands,
        New_Caledonia,
        New_Zealand,
        Nicaragua,
        Niger,
        Nigeria,
        Niue,
        Norfolk_Island,
        Norway,
        Oman,
        Pakistan,
        Palau,
        Panama,
        Papua_New_Guinea,
        Paraguay,
        Peru,
        Philippines,
        Pitcairn,
        Poland,
        Portugal,
        Puerto_Rico,
        Qatar,
        Reunion,
        Romania,
        Russia,
        Rwanda,
        Samoa,
        San_Marino,
        Sao_Tome_and_Principe,
        Saudi_Arabia,
        Senegal,
        Serbia,
        Seychelles,
        Sierra_Leone,
        Singapore,
        Slovakia,
        Slovenia,
        Solomon_Islands,
        Somalia,
        South_Africa,
        South_Sudan,
        Spain,
        Sri_Lanka,
        Sudan,
        Suriname,
        Swaziland,
        Sweden,
        Switzerland,
        Syria,
        Taiwan,
        Tajikistan,
        Tanzania, 
        Thailand,
        Timor_Leste,
        Togo,
        Tokelau,
        Tonga,
        Trinidad_and_Tobago,
        Tunisia,
        Turkey,
        Turkmenistan,
        Tuvalu,
        Uganda,
        Ukraine,
        United_Arab_Emirates,
        United_Kingdom,
        United_States,
        Uruguay,
        Uzbekistan,
        Vanuatu,
        Venezuela,
        Viet_Nam,
        Western_Sahara,
        Yemen,
        Zambia,
        Zimbabwe,
    }

    public class CountryInfo
    {
        public readonly Country country;
        public string name { get { return country.ToString().Replace('_', ' '); } }
        public readonly string code;
        public readonly Continent continent;

        public CountryInfo(Country country, string code, Continent continent)
        {
            this.country = country;
            this.code = code;
            this.continent = continent;
        }

        private static Dictionary<string, Country> _countriesByCode;

        private static Dictionary<Country, CountryInfo> _countries;
        public static IEnumerable<CountryInfo> countries
        {
		    get
            {
                if (_countries == null) {
                    InitCountries();
                }
                return _countries.Values;
            }
        }

        public static CountryInfo GetInfo(Country country)
        {
            if (_countries == null)
            {
                InitCountries();
            }

            if (_countries.ContainsKey(country))
            {
                return _countries[country];
            }

            return null;
        }

        public static Country FindCountryByCode(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return Country.Unknown;
            }

            if (_countriesByCode == null)
            {
                InitCountries();
            }

            code = code.ToUpper();

            if (_countriesByCode.ContainsKey(code))
            {
                return _countriesByCode[code];
            }

            return Country.Unknown;
        }

        private static void AddCountry(Country country, string code, Continent continent)
        {
            var c = new CountryInfo(country, code, continent);
            _countries[country] = c;
            _countriesByCode[code] = country;
        }

        private static void InitCountries()
        {
            _countries = new Dictionary<Country, CountryInfo>();
            _countriesByCode = new Dictionary<string, Country>();
            AddCountry(Country.Afghanistan, "AF", Continent.Asia);
            AddCountry(Country.Albania, "AL", Continent.Europe);
            AddCountry(Country.Algeria, "DZ", Continent.Africa);
            AddCountry(Country.Andorra, "AD", Continent.Europe);
            AddCountry(Country.Angola, "AO", Continent.Africa);
            AddCountry(Country.Argentina, "AR", Continent.South_America);
            AddCountry(Country.Armenia, "AM", Continent.Asia);
            AddCountry(Country.Australia, "AU", Continent.Oceania);
            AddCountry(Country.Austria, "AT", Continent.Europe);
            AddCountry(Country.Azerbaijan, "AZ", Continent.Asia);
            AddCountry(Country.Bahamas, "BS", Continent.North_America);
            AddCountry(Country.Bahrain, "BH", Continent.Asia);
            AddCountry(Country.Bangladesh, "BD", Continent.Asia);
            AddCountry(Country.Barbados, "BB", Continent.North_America);
            AddCountry(Country.Belarus, "BY", Continent.Europe);
            AddCountry(Country.Belgium, "BE", Continent.Europe);
            AddCountry(Country.Belize, "BZ", Continent.South_America);
            AddCountry(Country.Bhutan, "BT", Continent.Asia);
            AddCountry(Country.Bolivia, "BO", Continent.South_America);
            AddCountry(Country.Bosnia_and_Herzegovina, "BA", Continent.Europe);
            AddCountry(Country.Botswana, "BW", Continent.Africa);
            AddCountry(Country.Brazil, "BR", Continent.South_America);
            AddCountry(Country.Brunei, "BN", Continent.Asia);
            AddCountry(Country.Bulgaria, "BG", Continent.Europe);
            AddCountry(Country.Burkina_Faso, "BF", Continent.Africa);
            AddCountry(Country.Burundi, "BI", Continent.Africa);
            AddCountry(Country.Cambodia, "KH", Continent.Asia);
            AddCountry(Country.Cameroon, "CM", Continent.Africa);
            AddCountry(Country.Canada, "CA", Continent.North_America);
            AddCountry(Country.Cabo_Verde, "CV", Continent.Africa);
            AddCountry(Country.Cayman_Islands, "KY", Continent.North_America);
            AddCountry(Country.Chad, "TD", Continent.Africa);
            AddCountry(Country.Chile, "CL", Continent.South_America);
            AddCountry(Country.China, "CN", Continent.Asia);
            AddCountry(Country.Colombia, "CO", Continent.South_America);
            AddCountry(Country.Congo, "CG", Continent.Africa);
            AddCountry(Country.Costa_Rica, "CR", Continent.South_America);
   
            AddCountry(Country.Croatia, "HR", Continent.Europe);
            AddCountry(Country.Cuba, "CU", Continent.South_America);
            AddCountry(Country.Cyprus, "CY", Continent.Europe);
            AddCountry(Country.Czech_Republic, "CZ", Continent.Europe);
            AddCountry(Country.Denmark, "DK", Continent.Europe);
            AddCountry(Country.Djibouti, "DJ", Continent.Africa);
            AddCountry(Country.Dominica, "DM", Continent.South_America);
            AddCountry(Country.Dominican_Republic, "DO", Continent.South_America);
            AddCountry(Country.Ecuador, "EC", Continent.South_America);
            AddCountry(Country.Egypt, "EG", Continent.Africa);
            AddCountry(Country.El_Salvador, "SV", Continent.South_America);
            AddCountry(Country.Equatorial_Guinea, "GQ", Continent.Africa);
            AddCountry(Country.Eritrea, "ER", Continent.Africa);
            AddCountry(Country.Estonia, "EE", Continent.Europe);
            AddCountry(Country.Ethiopia, "ET", Continent.Africa);
            AddCountry(Country.Fiji, "FJ", Continent.Oceania);
            AddCountry(Country.Finland, "FI", Continent.Europe);
            AddCountry(Country.France, "FR", Continent.Europe);
            AddCountry(Country.Gabon, "GA", Continent.Africa);
            AddCountry(Country.Gambia, "GM", Continent.Africa);
            AddCountry(Country.Georgia, "GE", Continent.Asia);
            AddCountry(Country.Germany, "DE", Continent.Europe);
            AddCountry(Country.Ghana, "GH", Continent.Africa);
            AddCountry(Country.Gibraltar, "GI", Continent.Africa);
            AddCountry(Country.Greece, "GR", Continent.Europe);
            AddCountry(Country.Greenland, "GL", Continent.North_America);
            AddCountry(Country.Grenada, "GD", Continent.North_America);
            AddCountry(Country.Guatemala, "GT", Continent.South_America);
            AddCountry(Country.Guinea, "GN", Continent.Africa);
            AddCountry(Country.Guinea_Bissau, "GW", Continent.Africa);
            AddCountry(Country.Guyana, "GY", Continent.Africa);
            AddCountry(Country.Haiti, "HT", Continent.South_America);
            AddCountry(Country.Vatican, "VA", Continent.Europe);
            AddCountry(Country.Honduras, "HN", Continent.South_America);
            AddCountry(Country.Hong_Kong, "HK", Continent.Asia);
            AddCountry(Country.Hungary, "HU", Continent.Europe);
            AddCountry(Country.Iceland, "IS", Continent.Europe);
            AddCountry(Country.India, "IN", Continent.Asia);
            AddCountry(Country.Indonesia, "ID", Continent.Asia);
            AddCountry(Country.Iran, "IR", Continent.Asia);
            AddCountry(Country.Iraq, "IQ", Continent.Asia);
            AddCountry(Country.Ireland, "IE", Continent.Europe);
            AddCountry(Country.Israel, "IL", Continent.Asia);
            AddCountry(Country.Italy, "IT", Continent.Europe);
            AddCountry(Country.Jamaica, "JM", Continent.North_America);
            AddCountry(Country.Japan, "JP", Continent.Asia);
            AddCountry(Country.Jordan, "JO", Continent.Asia);
            AddCountry(Country.Kazakhstan, "KZ", Continent.Asia);
            AddCountry(Country.Kenya, "KE", Continent.Africa);
            AddCountry(Country.North_Korea, "KP", Continent.Asia);
            AddCountry(Country.South_Korea, "KR", Continent.Asia);
            AddCountry(Country.Kuwait, "KW", Continent.Asia);
            AddCountry(Country.Kyrgyzstan, "KG", Continent.Asia);
            AddCountry(Country.Lao, "LA", Continent.Asia);
            AddCountry(Country.Latvia, "LV", Continent.Europe);
            AddCountry(Country.Lebanon, "LB", Continent.Asia);
            AddCountry(Country.Lesotho, "LS", Continent.Africa);
            AddCountry(Country.Libya, "LY", Continent.Africa);
            AddCountry(Country.Liechtenstein, "LI", Continent.Europe);
            AddCountry(Country.Lithuania, "LT", Continent.Europe);
            AddCountry(Country.Luxembourg, "LU", Continent.Europe);
            AddCountry(Country.Macao, "MO", Continent.Asia);
            AddCountry(Country.Macedonia, "MK", Continent.Europe);
            AddCountry(Country.Madagascar, "MG", Continent.Asia);
            AddCountry(Country.Malaysia, "MY", Continent.Asia);
            AddCountry(Country.Maldives, "MV", Continent.North_America);
            AddCountry(Country.Mali, "ML", Continent.Africa);
            AddCountry(Country.Malta, "MT", Continent.Europe);
            AddCountry(Country.Mauritania, "MR", Continent.Africa);
            AddCountry(Country.Mauritius, "MU", Continent.Africa);
            AddCountry(Country.Mexico, "MX", Continent.South_America);
            AddCountry(Country.Moldova, "MD", Continent.Europe);
            AddCountry(Country.Monaco, "MC", Continent.Europe);
            AddCountry(Country.Mongolia, "MN", Continent.Asia);
            AddCountry(Country.Montenegro, "ME", Continent.Europe);
            AddCountry(Country.Morocco, "MA", Continent.Africa);
            AddCountry(Country.Mozambique, "MZ", Continent.Africa);
            AddCountry(Country.Myanmar, "MM", Continent.Asia);
            AddCountry(Country.Namibia, "NA", Continent.Africa);
            AddCountry(Country.Nauru, "NR", Continent.Africa);
            AddCountry(Country.Nepal, "NP", Continent.Asia);
            AddCountry(Country.Netherlands, "NL", Continent.Europe);
            AddCountry(Country.New_Zealand, "NZ", Continent.Oceania);
            AddCountry(Country.Nicaragua, "NI", Continent.South_America);
            AddCountry(Country.Niger, "NE", Continent.Africa);
            AddCountry(Country.Nigeria, "NG", Continent.Africa);
            AddCountry(Country.Norway, "NO", Continent.Europe);
            AddCountry(Country.Oman, "OM", Continent.Asia);
            AddCountry(Country.Pakistan, "PK", Continent.Asia);
            AddCountry(Country.Panama, "PA", Continent.South_America);
            AddCountry(Country.Paraguay, "PY", Continent.South_America);
            AddCountry(Country.Peru, "PE", Continent.South_America);
            AddCountry(Country.Philippines, "PH", Continent.North_America);
            AddCountry(Country.Poland, "PL", Continent.Europe);
            AddCountry(Country.Portugal, "PT", Continent.Europe);
            AddCountry(Country.Puerto_Rico, "PR", Continent.North_America);
            AddCountry(Country.Qatar, "QA", Continent.Asia);
            AddCountry(Country.Romania, "RO", Continent.Europe);
            AddCountry(Country.Russia, "RU", Continent.Europe);
            AddCountry(Country.Rwanda, "RW", Continent.Asia);
            AddCountry(Country.Samoa, "WS", Continent.Oceania);
            AddCountry(Country.San_Marino, "SM", Continent.Europe);
            AddCountry(Country.Sao_Tome_and_Principe, "ST", Continent.Africa);
            AddCountry(Country.Saudi_Arabia, "SA", Continent.Asia);
            AddCountry(Country.Senegal, "SN", Continent.Africa);
            AddCountry(Country.Serbia, "RS", Continent.Europe);
            AddCountry(Country.Seychelles, "SC", Continent.Africa);
            AddCountry(Country.Sierra_Leone, "SL", Continent.Africa);
            AddCountry(Country.Singapore, "SG", Continent.Asia);
            AddCountry(Country.Slovakia, "SK", Continent.Europe);
            AddCountry(Country.Slovenia, "SI", Continent.Europe);
            AddCountry(Country.Somalia, "SO", Continent.Africa);
            AddCountry(Country.South_Africa, "ZA", Continent.Africa);
            AddCountry(Country.South_Sudan, "SS", Continent.Asia);
            AddCountry(Country.Spain, "ES", Continent.Europe);
            AddCountry(Country.Sri_Lanka, "LK", Continent.Asia);
            AddCountry(Country.Sudan, "SD", Continent.Asia);
            AddCountry(Country.Suriname, "SR", Continent.Asia);
            AddCountry(Country.Swaziland, "SZ", Continent.Africa);
            AddCountry(Country.Sweden, "SE", Continent.Europe);
            AddCountry(Country.Switzerland, "CH", Continent.Europe);
            AddCountry(Country.Syria, "SY", Continent.Asia);
            AddCountry(Country.Taiwan, "TW", Continent.Asia);
            AddCountry(Country.Tajikistan, "TJ", Continent.Asia);
            AddCountry(Country.Tanzania, "TZ", Continent.Africa);
            AddCountry(Country.Thailand, "TH", Continent.Asia);
            AddCountry(Country.Timor_Leste, "TL", Continent.Asia);
            AddCountry(Country.Togo, "TG", Continent.Africa);
            AddCountry(Country.Tunisia, "TN", Continent.Africa);
            AddCountry(Country.Turkey, "TR", Continent.Europe);
            AddCountry(Country.Turkmenistan, "TM", Continent.Asia);
            AddCountry(Country.Uganda, "UG", Continent.Africa);
            AddCountry(Country.Ukraine, "UA", Continent.Europe);
            AddCountry(Country.United_Arab_Emirates, "AE", Continent.Asia);
            AddCountry(Country.United_Kingdom, "GB", Continent.Europe);
            AddCountry(Country.United_States, "US", Continent.North_America);
            AddCountry(Country.Uruguay, "UY", Continent.South_America);
            AddCountry(Country.Uzbekistan, "UZ", Continent.Asia);
            AddCountry(Country.Venezuela, "VE", Continent.South_America);
            AddCountry(Country.Viet_Nam, "VN", Continent.Asia);
            AddCountry(Country.Western_Sahara, "EH", Continent.Africa);
            AddCountry(Country.Yemen, "YE", Continent.Asia);
            AddCountry(Country.Zambia, "ZM", Continent.Africa);
            AddCountry(Country.Zimbabwe, "ZW", Continent.Africa);
        }
    }

}
