using System.Security.Cryptography;
using System.Text;

namespace dbcollector_api.Utils
{
    public class RSAHelper
    {
        private static string privateKey = "MIIEogIBAAKCAQEAw3QOHPeag9yhncP0gSRtbMeXu/5EmVexgovhrDQ9rhaAiLv32U5Yp0tcRsjPpAIuyh0/itVEOl3CCuqbR9yRZ4BAgTPe+8DjSq8FIIEgn3E4Q+OEXJZe9xXx7VzuIPKLb4HbPOzc0auIQWn96aCV52iCkUJUDVSPLHf+T6rEMCHESLIfjV0j+FAMBLdqCXJkaAqehJq9lamRRFYhDO7Gmx7oSPzErvASAcbua/u4nmgFlNcTeZYA6Arib52Ihs6zhH3zfX2OWg32vNKadjCBS1WAENNPGVYh+ajYLLJw8YB6AkKss1HxsuWSHzee9ke/KNhk6IGEEg2VVjftOYsyeQIDAQABAoIBADSh2+sRAhrgHuVND1p3ZMAWP1XwcqiYZMSyxmKI7tMiXBp2A5gQ8O6b7I1jJgcUT2U92w6Xv3e4J7io1IfVbZJhhv1D6pgAqTRDTS4jl5VX977QVaRZGdmPn0Y78CQPLF0qqs8NOal8d4Sl9iojhDp8eiyDn/629pcS+PibE5miBa+tMOvnPd/SF9wzHcQEhNQXR1Cwy/Dyh1HL+ku0XtJG6lItwIK73cgsOPJImx99UoCPqeHXhjXh+8+xcGo3D0h5KZYi05+93u4uDz6qc+CJM3RDt3eUnolH21R53rgkB06rqbT9B5zDBV6fdhF27SLkVdYHoUJtcA3cy70WucECgYEA3ihPuWRWZTJ+pxR597r4igODkN9rgUrP2VzjlYoB1+vF4Je8IDVoHMWZ7tIV+7hVjhoIsbHOVgSGU/fPQBZogZGmIkWrx+gMWUqI/oy7W51BvQTxtwRenhvCg5R8HJ3XcfYDRtdDAVy+9x2Sg7bk1CMv+WYUGlFUkt/68AlUVPMCgYEA4TpWUkzh7ra7d0x0Tuy7LOSzN8VT9IzJlX3L9Twn9rY6Nfq1lDiY5LKhflioCdYqzsciP6QEXAOo+ILBDVFsF38jN9Bbf2MhbmN6J3K3fUCSMgY0+lN0/UhWmOIsxGYwHkosegrX+1XDs9ERMb9QXu1EQr4cxUi/tFBz2yjAZeMCgYBszW9mS9boTxeqeqPViVOqPFhWPqXnN52eRhkMJwAKIOXTvlybpaxs0vY1+dxcYQY9x1BUvtFgXWzweOCe8ZqTQqMMC/U6vdI2dQOtL32fO+BzU5WSXeh9JPlZ+gHi/gcDQEqQimK9qw+39VrJeWyO3QDk83KLBSQXnuzGXLtZswKBgC1e5Ri7KCBAa09C9YMYqTQH9hpcA+eVnN4icz25motWdi99i6qKJDKd0W50SZWBsSnqb3nGfqJSkm1NWbpnFpE9KUkLDgOBYrCsFWVw9Imkwk6VdYKf4UdMlTVDCqWduD/BzWfgW1XkFwJYMVCGK5iTz1Zqmb0cRJH8SvpxISDlAoGAUWicp7IhVrkX/B4ViQ89eyIWIxuRKyTrJ1keo/Js1aD4WqdmV+Vyx7v9GUvG3K443HfB7skrCL29jr4SWcPc2i7HIvc+5RFG4MZWLXvg8Km9be/iU20JL0B4WjLRMYBlRAvrXKgplyCfalOjC9r2csQGEgtVITgxUeEb0vhx7K8=";
        public static (string publicKey, string privateKey) GenerateKeyPair()
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                var publicKey = Convert.ToBase64String(rsa.ExportRSAPublicKey());
                var privateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey());
                return (publicKey, privateKey);
            }
        }

        public static string EncryptWithPublicKey(string data, string publicKey)
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                rsa.ImportRSAPublicKey(Convert.FromBase64String(publicKey), out _);
                var dataBytes = Encoding.UTF8.GetBytes(data);
                var encryptedData = rsa.Encrypt(dataBytes, false);
                return Convert.ToBase64String(encryptedData);
            }
        }

        public static string DecryptWithPrivateKey(string encryptedData)
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                rsa.ImportRSAPrivateKey(Convert.FromBase64String(privateKey), out _);
                var dataBytes = Convert.FromBase64String(encryptedData);
                var decryptedData = rsa.Decrypt(dataBytes, false);
                return Encoding.UTF8.GetString(decryptedData);
            }
        }
    }
}
