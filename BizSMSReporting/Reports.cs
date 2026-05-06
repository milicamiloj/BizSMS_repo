using ClosedXML.Excel;
using Microsoft.Extensions.Configuration;
using System;
using System.Data;
using System.Data.SqlClient;

namespace BizSMSReporting
{
    class Reports
    {
        private IConfiguration _appsettingValues;
        public Reports(IConfiguration configuration)
        {
            _appsettingValues = configuration;
        }

        public void CreateMonthlyReport(int month, int year, string filePath)
        {
            try
            {
                //poziv procedure u bazi za kreiranje izvestaja
                DataSet ds = CallToProcedureBizSMSReport(month, year);

                //export tabele u excel i smestanje na zadatu lokaciju
                ExportXLSX(ds.Tables[0], filePath);
            }
            catch (Exception error)
            {
                throw new ApplicationException("Greska prilikom kreiranja izvestaja za BizSMS: " + error);
            }
        }

        private DataSet CallToProcedureBizSMSReport(int month, int year)
        {
            DataSet ds = new DataSet();
            try
            {
                using SqlConnection conn = new SqlConnection(_appsettingValues.GetSection("BizSMSConnectionString").Value);
                conn.Open();

                SqlCommand cmd = new SqlCommand("sp_BizSMSReport", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                SqlParameter Month = new SqlParameter("@iMonth", SqlDbType.Int);
                cmd.Parameters.Add(Month);
                cmd.Parameters["@iMonth"].Value = month;

                SqlParameter Year = new SqlParameter("@iYear", SqlDbType.Int);
                cmd.Parameters.Add(Year);
                cmd.Parameters["@iYear"].Value = year;

                SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                adapter.Fill(ds, "MonthlyReportBizSMS");
                conn.Close();
            }
            catch (Exception error)
            {
                throw new ApplicationException("Greska u bazi prilikom pozivanja sp_BizSMSReport: " + error);
            }

            return ds;
        }

        private void ExportXLSX(DataTable queryResultDT, string filePath)
        {
            try
            {
                //var filePath = folderName + fileName;
                var numberOfRowsPlusHeader = queryResultDT.Rows.Count + 1;

                using var wb = new XLWorkbook();
                var workSheet = wb.Worksheets.Add("Izvestaj");
                workSheet.FirstCell().InsertTable(queryResultDT, false);
                workSheet.Columns("A", "B").AdjustToContents();
                IXLRange header = workSheet.Range(workSheet.Cell(1, 1).Address, workSheet.Cell(1, 2).Address);
                IXLRange body = workSheet.Range(workSheet.Cell(2, 1).Address, workSheet.Cell(numberOfRowsPlusHeader, 2).Address);
                header.Style.Font.SetBold();
                header.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
                header.Style.Border.InsideBorder = XLBorderStyleValues.Medium;
                body.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
                body.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                wb.SaveAs(filePath);
            }
            catch (Exception error)
            {
                throw new ApplicationException("Greska prilikom formiranja excel dokumenta sa izvestajem za BizSMS: " + error);
            }
        }
    }
}
