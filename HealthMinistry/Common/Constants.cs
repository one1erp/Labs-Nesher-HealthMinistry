using System.Collections.Generic;


namespace HealthMinistry.Common
{
    public static class ConstantsMsg
    {


        public static readonly List<string> msgList = new List<string>()
                {
                    "לא נמצאה תעודה עם המזהה שנשלח",
                    "התעודה לא מאושרת,לא ניתן לשלוח למשרד הבריאות",
                    "לתעודה של דרישה זו pdf לא נמצא מסמך ",
                    "שגיאה בשליחת הבקשה למשרד הבריאות",
                    "התהליך עבר בהצלחה",
                    "failed",
                    "נכשלה יצירת XML ראשוני לשליחה"
                };

        //validation tests
        public static List<string> _ValidationMsgs = new List<string>()
            {
                "לקוח לא קיים, אנא פנה למנהל מערכת",
                "לא נמצאה בדיקה או מזהה בדיקה לא קיים, אנא פנה למנהל מערכת",
                "מזהה בדיקה לא קיים, אנא פנה למנהל מערכת",
                "אין תמיכה בתצורה הישנה",
                ",הזמנה חוזרת אפשרית רק במקרה של ביטול הקודמת.ההזמנה כבר קיימת במערכת"
            };
    }


}

