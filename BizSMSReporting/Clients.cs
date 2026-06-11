using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace BizSMSReporting
{
    class Clients
    {
        private readonly IConfiguration _appsettingValues;
        public Clients(IConfiguration configuration)
        {
            _appsettingValues = configuration;
        }
        public List<string> GetNewClientIds()
        {
            DataTable dt = new DataTable();
            List<string> clientIds = new List<string>();

            try
            {
                using SqlConnection conn = new SqlConnection(_appsettingValues.GetSection("BizSMSConnectionString").Value);
                conn.Open();

                SqlCommand cmd = new SqlCommand("sp_NewClients", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.ExecuteNonQuery();

                SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                adapter.Fill(dt);
                conn.Close();

                //kod treba biti u formatu od 5 cifara gde nule dopunjuju prazna mesta do 5
                clientIds.AddRange(from DataRow row in dt.Rows
                                   select row[0].ToString().PadLeft(5, '0'));
            }
            catch (Exception error)
            {
                throw new ApplicationException("Greska u bazi prilikom pozivanja sp_NewClients: " + error);
            }

            return clientIds;
        }
    }
}
