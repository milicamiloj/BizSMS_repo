#nullable disable
using System;
using System.ComponentModel.DataAnnotations;

namespace BizSMS.Web.Models
{
    // Zamena za legacy BizSMS.Models.SentMessage*/SentSMSJob* view-modele (Models/SendSMSViewModels.cs).
    // ResourceType je zamenjen plain srpskim Display stringovima (po odluci migracije, bez Resources projekta).
    public class SentMessageReportViewModel
    {
        public int SendYear { get; set; }
        public int SendMonth { get; set; }

        [Display(Name = "Mesec")]
        public string SendMonthName { get; set; }

        [Display(Name = "Tip broja")]
        public string NumberTypeName { get; set; }

        [Display(Name = "Broj dostavljenih SMS poruka")]
        public int NumberOfDeliveredMessages { get; set; }

        [Display(Name = "Suma dostavljenih SMS-ova (uključujući i višedelne poruke)")]
        public int NumberOfDeliveredMessagesLength { get; set; }

        [Display(Name = "Broj poslatih SMS poruka")]
        public int NumberOfSentSMSes { get; set; }

        [DisplayFormat(DataFormatString = "{0:#,##0.00#}")]
        [Display(Name = "Cena")]
        public double Cost { get; set; }

        public bool Charged { get; set; }
    }

    public class SentMessageDetailReportViewModel
    {
        [Display(Name = "Broj")]
        public string Number { get; set; }

        [Display(Name = "Tip broja")]
        public string NumberTypeName { get; set; }

        [Display(Name = "Broj poslatih SMS poruka")]
        public int NumberOfMessages { get; set; }

        [Display(Name = "Broj dostavljenih SMS poruka")]
        public int NumberOfDelivered { get; set; }

        [Display(Name = "Suma dostavljenih SMS-ova (uključujući i višedelne poruke)")]
        public int NumberOfDeliveredMessagesLength { get; set; }

        [Display(Name = "Ukupna cena (uključujući i višedelne poruke)")]
        public double PriceOfDeliveredMessagesLength { get; set; }
    }

    public class SentSMSJobReportViewModel
    {
        public int ID { get; set; }

        [Display(Name = "Korisnik")]
        public string User { get; set; }

        [Display(Name = "Alfanumerik")]
        public string Alphanumeric { get; set; }

        [Display(Name = "Datum slanja")]
        public DateTime SendDate { get; set; }

        [Display(Name = "VPN")]
        public int VPN { get; set; }

        [Display(Name = "U MTS")]
        public int InMTS { get; set; }

        [Display(Name = "Van MTS")]
        public int OutMTS { get; set; }

        [Display(Name = "Tekst poruke")]
        public string Message { get; set; }

        public string Status { get; set; }

        [Display(Name = "Otkazao")]
        public string CanceledBy { get; set; }
    }

    public class SentSMSJobDetailReportViewModel
    {
        [Display(Name = "Broj")]
        public string Number { get; set; }

        [Display(Name = "Tip broja")]
        public string NumberTypeName { get; set; }

        [Display(Name = "Naziv")]
        public string Name { get; set; }

        [Display(Name = "Tekst poruke")]
        public string MessageText { get; set; }

        [Display(Name = "Dužina poruke")]
        public int MessageLength { get; set; }

        [Display(Name = "Poslata")]
        public string Status { get; set; }

        [Display(Name = "Dostavljena")]
        public string Delivered { get; set; }
    }
}
