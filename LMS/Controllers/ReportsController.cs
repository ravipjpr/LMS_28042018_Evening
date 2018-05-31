using CLSLms;
using iTextSharp.text;
using iTextSharp.text.pdf;
using LMS.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.UI;
using System.Web.UI.DataVisualization.Charting;
using System.Web.UI.WebControls;

namespace LMS.Controllers
{
    [CustomAuthorize]
    public class ReportsController : Controller
    {

        private LeopinkLMSDBEntities db = new LeopinkLMSDBEntities();

        #region // Summary report
        public ActionResult Index()
        {
            var ObjSummary = db.Rp_SummaryReport(Convert.ToInt32(Session["UserID"])).ToList();
            var Obj = from x in ObjSummary
                      select new ReportSummary
                      {
                          ActiveUsers = (x.ActiveUsers == null) ? 0 : (int)x.ActiveUsers,
                          Completedcourse = (int)x.Completedcourse,
                          NeverLoggedIn = (x.NeverLoggedIn == null) ? 0 : (int)x.NeverLoggedIn,
                          TotalActiveCategory = (x.TotalActiveCategory == null) ? 0 : (int)x.TotalActiveCategory,
                          TotalActiveCourses = (x.TotalActiveCourses == null) ? 0 : (int)x.TotalActiveCourses,
                          TotalActiveGroup = (x.TotalActiveCourses == null) ? 0 : (int)x.TotalActiveCourses,
                          TotalActiveOrganisation = (x.TotalActiveCourses == null) ? 0 : (int)x.TotalActiveCourses,
                          TotalAssignments = (int)x.TotalAssignments,
                          TotalCategory = (x.TotalCategory == null) ? 0 : (int)x.TotalCategory,
                          TotalCourses = (x.TotalCourses == null) ? 0 : (int)x.TotalCourses,
                          TotalGroup = (x.TotalGroup == null) ? 0 : (int)x.TotalGroup,
                          TotalOrganisation = (x.TotalOrganisation == null) ? 0 : (int)x.TotalOrganisation,
                          TotalUsers = (x.TotalUsers == null) ? 0 : (int)x.TotalUsers
                      };
            return View(Obj.FirstOrDefault());
        }
        #endregion

        #region // User group report
        public ActionResult UserReport()
        {
            Rp_UserReport usr = new Rp_UserReport();
            usr.GroupId = 0;
            bool IsGroupAdmin = (Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(2);
            var currentuser = db.UserProfiles.Find(Convert.ToInt64(Session["UserID"]));
            if (IsGroupAdmin)
            {
                var GroupAdminGroups = db.UserGroups.Where(x => x.UserId == currentuser.UserId).Select(x => x.GroupID).ToList();
                ViewBag.GroupsList = new SelectList(db.Groups.Where(x => x.IsDeleted == false && GroupAdminGroups.Contains(x.GroupID)).OrderBy(x => x.GroupName).Select(grp => grp), "GroupID", "GroupName");
                ViewBag.OrganisationList = new SelectList(db.Organisations.Where(x => x.IsDeleted == false && x.OrganisationID == currentuser.OrganisationID).Select(org => org), "OrganisationID", "OrganisationName");
            }
            else
            {
                ViewBag.GroupsList = new SelectList(db.Groups.Where(x => x.IsDeleted == false).OrderBy(x => x.GroupName).Select(grp => grp), "GroupID", "GroupName");
                ViewBag.OrganisationList = new SelectList(db.Organisations.Where(x => x.IsDeleted == false).OrderBy(x => x.OrganisationName).Select(org => org), "OrganisationID", "OrganisationName");
            }

            var result = db.Rp_UserReport(0, Convert.ToInt32(Session["UserID"])).ToList();
            ViewBag.totalOrganisations = result.Select(x => x.OrganisationName).Distinct().Count();
            ViewBag.totalGroups = result.Select(x => x.UserGroups).Distinct().Count();
            ViewBag.totalUsers = result.Select(x => x.EmployeeID).Distinct().Count();

            return View();
        }

        [HttpPost]
        public ActionResult UserReport(Rp_UserReport Rp_Object, [System.Web.Http.FromBody]string ActionType)
        {
            if (Rp_Object.GroupId == null) Rp_Object.GroupId = 0;
            if (ActionType == "1")
            {
                return UserReportPdf(Rp_Object);
            }
            else
            {
                var result = db.Rp_UserReport(Rp_Object.GroupId, Convert.ToInt32(Session["UserID"])).ToList();
                UserReportExcel(result);
            }

            bool IsGroupAdmin = (Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(2);
            var currentuser = db.UserProfiles.Find(Convert.ToInt64(Session["UserID"]));
            if (IsGroupAdmin)
            {
                var GroupAdminGroups = db.UserGroups.Where(x => x.UserId == currentuser.UserId).Select(x => x.GroupID).ToList();
                ViewBag.GroupsList = new SelectList(db.Groups.Where(x => x.IsDeleted == false && GroupAdminGroups.Contains(x.GroupID)).OrderBy(x => x.GroupName).Select(grp => grp), "GroupID", "GroupName");
            }
            else
                ViewBag.GroupsList = new SelectList(db.Groups.Where(x => x.IsDeleted == false).OrderBy(x => x.GroupName).Select(grp => grp), "GroupID", "GroupName");

            return View(Rp_Object);
        }

        private Byte[] UserGroupChart(double[] ChartData, string[] ChartDataWithLegend)
        {

            var chart = new Chart
            {
                Width = 600,
                Height = 315,
                RenderType = RenderType.ImageTag,
                AntiAliasing = AntiAliasingStyles.All,
                TextAntiAliasingQuality = TextAntiAliasingQuality.High

            };



            chart.Titles.Add("");
            chart.ChartAreas.Add("");
            chart.ChartAreas[0].BackColor = System.Drawing.Color.White;
            chart.ChartAreas[0].BorderColor = System.Drawing.Color.Green;
            chart.ChartAreas[0].InnerPlotPosition.Width = 100f;
            chart.ChartAreas[0].InnerPlotPosition.Height = 75f;
            //chart.ChartAreas[0].Area3DStyle.Enable3D = true;

            chart.Series.Add("");
            chart.Series[0].ChartType = SeriesChartType.Pie;
            chart.Series[0]["PieLabelStyle"] = "Outside";
            chart.Series[0].BorderWidth = 1;
            chart.Series[0].BorderColor = System.Drawing.Color.Black;
            chart.Series[0].LegendText = "#VALX (#PERCENT)";




            // Set the pie label as well as legend text to be displayed as percentage
            // The P2 indicates a precision of 2 decimals
            chart.Series[0].Label = "#VALX";

            // By sorting the data points, they show up in proper ascending order in the legend
            chart.DataManipulator.Sort(PointSortOrder.Descending, chart.Series[0]);

            chart.Legends.Add("Legend1");
            chart.Legends[0].Enabled = true;
            chart.Legends[0].Docking = Docking.Right;
            chart.Legends[0].Alignment = System.Drawing.StringAlignment.Center;


            //double[] yearlySales = { 93, 100, 22, 23, 43, 54, 44, 9, 10, 1 };
            //double[] ChartData = new double[] { Convert.ToInt32(Obj.NotStarted), Convert.ToInt32(Obj.InCompleted), Convert.ToInt32(Obj.Completed) };

            // Create a list of data
            //string[] salesPeopleTop10 = { "John Smith", "Patrick Johnson", "Michael Berube", "Paul Bradshaw", "Jacob Wright", "Jonathan Rosen", "Robert McDonald", "Joseph Hanson", "Marcel Thompson", "Trey Kelley" };
            //string[] ChartDataWithLegend = new string[] { LMSResourse.Admin.Report.thNotStarted + " : " + Obj.NotStarted, LMSResourse.Admin.Report.thIncomplete + " : " + Obj.InCompleted, LMSResourse.Admin.Report.thCompleted + " : " + Obj.Completed };

            chart.Titles[0].Font = new System.Drawing.Font("Arial", 10f);


            int index = 0;
            int recordsInChart = 0;
            foreach (string chartDataPoints in ChartDataWithLegend)
            {
                if (ChartData[index] != 0)
                {
                    chart.Series[0].Points.AddXY(chartDataPoints, ChartData[index]);
                    recordsInChart++;
                }
                index++;
            }


            using (var chartimage = new MemoryStream())
            {
                chart.SaveImage(chartimage, ChartImageFormat.Png);
                chart.Dispose();
                return chartimage.GetBuffer();
            }
        }

        public FilePathResult UserReportPdf(Rp_UserReport Rp_Object)
        {
            var doc = new Document(PageSize.A4); // set the paper size of pdf file.
            string filename = (Session["UserID"]).ToString() + "_UserGroup" + ".pdf";

            var pdf = Server.MapPath("../Content/Charts/" + filename);

            PdfWriter.GetInstance(doc, new FileStream(pdf, FileMode.Create)); // create a instance of pdf file.
            doc.Open();

            var UserChartData = db.Rp_UserReportChart(Rp_Object.GroupId, Convert.ToInt32(Session["UserID"])).ToList();
            if (UserChartData.Count > 0)
            {
                var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10); // Set the normal font
                var boldFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10); // set the normal font with bold
                var boldFontWhite = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, iTextSharp.text.Color.WHITE); // set the normal font with white background.

                var normalFontHeading = FontFactory.GetFont(FontFactory.HELVETICA, 12); // Set the font for heading
                var boldFontHeading = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12); // Set the font for heading with bold 

                int rowNumber = 0;
                foreach (var z in UserChartData.Select(x => x.GroupName).Distinct().ToList())
                {

                    doc.Add(new Chunk("   " + LMSResourse.Admin.Organisation.thGroups + ": " + z));
                    var chartData = UserChartData.Where(x => x.GroupName == z).Select(x => (double)x.UserCount).ToArray();
                    var ChartDataWithLegend = UserChartData.Where(x => x.GroupName == z).Select(x => x.GroupName + " : " + x.UserCount).ToArray();
                    var image = iTextSharp.text.Image.GetInstance(UserGroupChart(chartData, ChartDataWithLegend));

                    image.ScalePercent(75f); // set the image size on page

                    doc.Add(image); // add the chart image.
                    rowNumber++;
                    iTextSharp.text.Table docTable = new iTextSharp.text.Table(3, 5);
                    //docTable.Alignment = Element.ALIGN_CENTER;
                    docTable.DefaultVerticalAlignment = Element.ALIGN_MIDDLE;
                    //docTable.DefaultHorizontalAlignment = Element.ALIGN_CENTER;
                    docTable.Cellspacing = 1;
                    docTable.BorderColor = iTextSharp.text.Color.WHITE;
                    //docTable.DefaultCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                    docTable.DefaultCell.Border = 0;
                    docTable.AddCell(new Phrase(new Chunk(LMSResourse.Admin.Group.thGroup, boldFont)));
                    docTable.AddCell(new Phrase(new Chunk(LMSResourse.Admin.Report.thNumber, boldFont)));
                    docTable.AddCell(new Phrase(new Chunk(LMSResourse.Admin.Report.ThPercent, boldFont)));

                    foreach (var tabledata in UserChartData.Where(x => x.GroupName == z).Select(x => x).Distinct().ToList())
                    {
                        docTable.AddCell(new Phrase(new Chunk(tabledata.GroupName, normalFont)));
                        docTable.AddCell(new Phrase(new Chunk(tabledata.UserCount.ToString(), normalFont)));
                        docTable.AddCell(new Phrase(new Chunk(String.Format("{0,5:N1}", Convert.ToDecimal(Convert.ToDecimal((decimal)tabledata.UserCount / (decimal)UserChartData.Where(x => x.GroupName == z).Sum(x => x.UserCount))) * 100), normalFont)));
                    }
                    // Add the detail data in pdf file.
                    doc.Add(docTable);
                    doc.NewPage();
                    rowNumber++;
                }
            }
            else
            {
                var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10); // Set the normal font
                var boldFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10); // set the normal font with bold
                var boldFontWhite = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, iTextSharp.text.Color.WHITE); // set the normal font with white background.

                var normalFontHeading = FontFactory.GetFont(FontFactory.HELVETICA, 12); // Set the font for heading
                var boldFontHeading = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12); // Set the font for heading with bold 

                iTextSharp.text.Table docTable = new iTextSharp.text.Table(3, 5);
                //docTable.Alignment = Element.ALIGN_CENTER;
                docTable.DefaultVerticalAlignment = Element.ALIGN_MIDDLE;
                docTable.DefaultHorizontalAlignment = Element.ALIGN_CENTER;
                docTable.Cellspacing = 1;
                docTable.BorderColor = iTextSharp.text.Color.WHITE;
                docTable.DefaultCell.Border = 0;
                docTable.AddCell(new Phrase(new Chunk("No data exist", boldFont)));
                doc.Add(docTable);
            }
            doc.Close();
            return File(pdf, "application/pdf", "Chart_UserGroupReport.pdf"); // return the pdf file.
        }

        public void UserReportExcel(List<Rp_UserReport_Result> result)
        {
            GridView gv = new GridView();
            if (result.Count() < 0)
            {
                gv.EmptyDataText = LMSResourse.Common.Common.lblBlankdata;
                gv.DataSource = null;
            }
            else
            {
                gv.DataSource = result;
                gv.DataBind();
            }
            Response.ClearContent();
            Response.Buffer = true;
            Response.AddHeader("content-disposition", "attachment; filename=Userreport.xls");
            Response.ContentType = "application/ms-excel";
            Response.Charset = "";
            StringWriter sw = new StringWriter();
            HtmlTextWriter htw = new HtmlTextWriter(sw);
            gv.RenderControl(htw);
            Response.Output.Write(sw.ToString());
            Response.Flush();
            Response.End();
        }

        public ActionResult AjaxHandlerUserReport(jQueryDataTableParamModel param)
        {
            var Params = param.fCol1.Split('~');

            var returnResult = (from obj in db.GetUserCourseReport(Convert.ToInt32(Params[0]), 0, Convert.ToInt32(Params[1]), Convert.ToInt32(Session["UserID"]), false, 0, "0")
                                group obj by new
                                {
                                    obj.EmailAddress,
                                    obj.FirstName,
                                    obj.LastName
                                }
                         into gs
                                select new UserReport
                                {
                                    Email = gs.Key.EmailAddress,
                                    FirstName = gs.Key.FirstName,
                                    LastName = gs.Key.LastName,
                                    Courses = gs.Count(c => c.CourseName != null),
                                    Completed = gs.Count(c => c.Status == "Completed"),
                                    Notstarted = gs.Count(c => c.Status == "Not started"),
                                    Inprogress = gs.Count(c => c.Status == "In progress")

                                }).ToList();


            // records to display            
            var displayedRecord = returnResult.Skip(param.iDisplayStart).Take(param.iDisplayLength);
            if (param.iDisplayLength == -1)
                displayedRecord = returnResult;

            var result = from obj in displayedRecord
                         select new[] {
                              obj.Email,
                              obj.FirstName+" "+obj.LastName,
                              obj.Courses.ToString(),
                              obj.Completed.ToString(),
                              obj.Inprogress.ToString(),
                              obj.Notstarted.ToString(),
                              db.UserProfiles.Where(u=>u.EmailAddress==obj.Email).Select(u=>u.UserId).FirstOrDefault().ToString()
                          };

            return Json(new
            {
                sEcho = param.sEcho,
                iTotalRecords = returnResult.Count(),
                iTotalDisplayRecords = returnResult.Count(),
                aaData = result
            },
                           JsonRequestBehavior.AllowGet);
        }

        public ActionResult UserReportSummary(Rp_UserReport Rp_Object)
        {
            return View();
        }
        #endregion

        #region // User Course report
        public ActionResult UserCourseReport()
        {
            Rp_UserReport usr = new Rp_UserReport();
            usr.GroupId = 0;
            bool IsGroupAdmin = (Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(2);
            var currentuser = db.UserProfiles.Find(Convert.ToInt64(Session["UserID"]));
            if (IsGroupAdmin)
            {
                var GroupAdminGroups = db.UserGroups.Where(x => x.UserId == currentuser.UserId).Select(x => x.GroupID).ToList();
                ViewBag.GroupsList = new SelectList(db.Groups.Where(x => x.IsDeleted == false && GroupAdminGroups.Contains(x.GroupID)).OrderBy(x => x.GroupName).Select(grp => new UserGroupsLocal { GroupId = grp.GroupID.ToString() + "~" + grp.OrganisationID.ToString(), GroupName = grp.GroupName }), "GroupId", "GroupName");
                ViewBag.OrganisationList = new SelectList(db.Organisations.Where(x => x.IsDeleted == false && x.OrganisationID == currentuser.OrganisationID).Select(org => org), "OrganisationID", "OrganisationName");
            }
            else
            {
                ViewBag.GroupsList = new SelectList(db.Groups.Where(x => x.IsDeleted == false).OrderBy(x => x.GroupName).Select(grp => new UserGroupsLocal { GroupId = grp.GroupID.ToString() + "~" + grp.OrganisationID.ToString(), GroupName = grp.GroupName }), "GroupId", "GroupName");
                ViewBag.OrganisationList = new SelectList(db.Organisations.Where(x => x.IsDeleted == false).OrderBy(x => x.OrganisationName).Select(org => org), "OrganisationID", "OrganisationName");
            }
            IEnumerable<Course> filterCourse = null;
            var currentLoginUser = Convert.ToInt64(Session["UserID"].ToString());

            var Groups = from g in db.UserGroups
                         where g.UserId == currentLoginUser
                         select g.GroupID;
            List<int> lst = Groups.ToList();
            var userrole = Session["UserRoles"].ToString().Contains("1");
            if (userrole)
            {
                filterCourse = from Cour in db.Courses
                               where Cour.IsDeleted == false && Cour.IsFinalized == true
                               orderby Cour.CourseName.ToLower()
                               select Cour;
            }
            else
            {
                if (currentuser.Organisation.IsUserAssignment)
                {
                    var liUserID = (from uf in db.UserProfiles where uf.OrganisationID == currentuser.OrganisationID && uf.IsDelete == false && uf.Status == true select uf.UserId).ToList();

                    filterCourse = ((from Cour in db.Courses
                                     join userCrs in db.UserCourses
                                     on Cour.CourseId equals userCrs.CourseId
                                     where Cour.IsDeleted == false && Cour.IsFinalized == true && userCrs.AssignedStatus == true && liUserID.Contains(userCrs.UserId)
                                     orderby Cour.CourseName.ToLower()
                                     select Cour).Union
                               (
                                from Crs in db.Courses
                                where Crs.IsDeleted == false && Crs.IsFinalized == true && Crs.CreatedById == currentLoginUser
                                orderby Crs.CourseName.ToLower()
                                select Crs
                               )).Distinct();

                }
                else
                {
                    filterCourse = ((from Cour in db.Courses
                                     join GrpCrs in db.GroupCourses
                                     on Cour.CourseId equals GrpCrs.CourseId
                                     where Cour.IsDeleted == false && Cour.IsFinalized == true && GrpCrs.AssignedStatus == true && lst.Contains(GrpCrs.GroupID)
                                     orderby Cour.CourseName.ToLower()
                                     select Cour).Union
                               (
                                from Crs in db.Courses
                                where Crs.IsDeleted == false && Crs.IsFinalized == true && Crs.CreatedById == currentLoginUser
                                orderby Crs.CourseName.ToLower()
                                select Crs
                               )).Distinct();
                }

            }

            int?[] totalUsers = new int?[12];
            decimal?[] totalPassedUsers = new decimal?[12];
            int?[] totalRegisteredUsers = new int?[12];


            for (int i = 1; i <= 12; i++)
            {
                var userCoursePassPercentage = db.GetUserCoursePassPercentageMonthwise(i, false).FirstOrDefault();
                totalUsers[i - 1] = userCoursePassPercentage.TotalUsers;
                totalPassedUsers[i - 1] = userCoursePassPercentage.TotalUserCoursePassPercentage;
                totalRegisteredUsers[i - 1] = userCoursePassPercentage.TotalRegisteredUsers;
            }

            ViewBag.TotalUsers = totalUsers;
            ViewBag.TotalPassedUsers = totalPassedUsers;
            ViewBag.TotalRegisteredUsers = totalRegisteredUsers;
            ViewBag.CourseList = new SelectList(filterCourse.OrderBy(x => x.CourseName).Select(x => x), "CourseId", "CourseName");
            return View();
        }

        [HttpPost]
        public ActionResult UserCourseReport(Rp_UserCourseReport Rp_Object)
        {
            if (Rp_Object.GroupId == null) Rp_Object.GroupId = 0;
            if (Rp_Object.OrganisationId == null) Rp_Object.OrganisationId = 0;
            if (Rp_Object.CourseId == null) Rp_Object.CourseId = 0;

            //var result = db.Rp_UserReport(Rp_Object.GroupId, Convert.ToInt32(Session["UserID"])).ToList();
            var result = db.GetUserCourseReport(Rp_Object.OrganisationId, Rp_Object.CourseId, Rp_Object.GroupId, Convert.ToInt32(Session["UserID"]), Rp_Object.IsExpiryCourse, 0, "").ToList();
            UserCourseReportExcel(result);


            bool IsGroupAdmin = (Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(2);
            var currentuser = db.UserProfiles.Find(Convert.ToInt64(Session["UserID"]));
            if (IsGroupAdmin)
            {
                var GroupAdminGroups = db.UserGroups.Where(x => x.UserId == currentuser.UserId).Select(x => x.GroupID).ToList();
                ViewBag.GroupsList = new SelectList(db.Groups.Where(x => x.IsDeleted == false && GroupAdminGroups.Contains(x.GroupID)).OrderBy(x => x.GroupName).Select(grp => new { GroupID = (Convert.ToString(grp.GroupID) + "~" + Convert.ToString(grp.OrganisationID)), GroupName = grp.GroupName }), "GroupID", "GroupName");
                ViewBag.OrganisationList = new SelectList(db.Organisations.Where(x => x.IsDeleted == false && x.OrganisationID == currentuser.OrganisationID).Select(org => org), "OrganisationID", "OrganisationName");
            }
            else
            {
                ViewBag.GroupsList = new SelectList(db.Groups.Where(x => x.IsDeleted == false).OrderBy(x => x.GroupName).Select(grp => new UserGroupsLocal { GroupId = grp.GroupID.ToString() + "~" + grp.OrganisationID.ToString(), GroupName = grp.GroupName }), "GroupId", "GroupName");
                ViewBag.OrganisationList = new SelectList(db.Organisations.Where(x => x.IsDeleted == false).OrderBy(x => x.OrganisationName).Select(org => org), "OrganisationID", "OrganisationName");
            }
            ViewBag.CourseList = new SelectList(db.Courses.Where(x => x.IsDeleted == false && x.IsFinalized == true).OrderBy(x => x.CourseName).Select(x => x), "CourseId", "CourseName");
            return View(Rp_Object);
        }

        public void UserCourseReportExcel(List<GetUserCourseReport_Result> result)
        {
            GridView gv = new GridView();
            if (result.Count() < 0)
            {
                gv.EmptyDataText = LMSResourse.Common.Common.lblBlankdata;
                gv.DataSource = null;
            }
            else
            {
                gv.DataSource = result;
                gv.DataBind();
            }
            Response.ClearContent();
            Response.Buffer = true;
            Response.AddHeader("content-disposition", "attachment; filename=UserCoursereport.xls");
            Response.ContentType = "application/ms-excel";
            Response.Charset = "";
            StringWriter sw = new StringWriter();
            HtmlTextWriter htw = new HtmlTextWriter(sw);
            gv.RenderControl(htw);
            Response.Output.Write(sw.ToString());
            Response.Flush();
            Response.End();
        }

        public ActionResult AjaxHandlerUserCourseReport(jQueryDataTableParamModel param)
        {
            var Params = param.fCol1.Split('~');
            var returnResult = db.GetUserCourseReport(Convert.ToInt32(Params[0]), Convert.ToInt32(Params[1]), Convert.ToInt32(Params[2]), Convert.ToInt32(Session["UserID"]), false,0,"").ToList();


            // records to display            
            var displayedRecord = returnResult.Skip(param.iDisplayStart).Take(param.iDisplayLength);
            if (param.iDisplayLength == -1)
                displayedRecord = returnResult;

            var result = from obj in displayedRecord
                         select new[] {
                              obj.CourseName,
                              obj.FirstName,
                              obj.LastName,
                              obj.EmailAddress,
                              obj.RegistrationDate.HasValue?string.Format("{0:dd/MM/yyyy}",obj.RegistrationDate):"",
                              obj.Status,
                              obj.Completiondate.HasValue?string.Format("{0:dd/MM/yyyy}",obj.Completiondate):"",
                              obj.Score.ToString(),
                              obj.ExpiryDate.HasValue?string.Format("{0:dd/MM/yyyy}",obj.ExpiryDate):""
                          };

            return Json(new
            {
                sEcho = param.sEcho,
                iTotalRecords = returnResult.Count(),
                iTotalDisplayRecords = returnResult.Count(),
                aaData = result
            },
                           JsonRequestBehavior.AllowGet);
        }
        #endregion

        #region // Course report
        public ActionResult CourseReport()
        {
            Rp_CourseReport usr = new Rp_CourseReport();
            usr.GroupId = 0;
            bool IsGroupAdmin = (Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(2);
            var currentuser = db.UserProfiles.Find(Convert.ToInt64(Session["UserID"]));
            if (IsGroupAdmin)
            {
                var GroupAdminGroups = db.UserGroups.Where(x => x.UserId == currentuser.UserId).Select(x => x.GroupID).ToList();
                ViewBag.GroupsList = new SelectList(db.Groups.Where(x => x.IsDeleted == false && GroupAdminGroups.Contains(x.GroupID)).OrderBy(x => x.GroupName).Select(grp => new UserGroupsLocal { GroupId = (grp.GroupID.ToString() + "~" + grp.OrganisationID.ToString()), GroupName = grp.GroupName }), "GroupId", "GroupName");
                ViewBag.OrganisationList = new SelectList(db.Organisations.Where(x => x.IsDeleted == false && x.OrganisationID == currentuser.OrganisationID).Select(org => org), "OrganisationID", "OrganisationName");
            }
            else
            {
                ViewBag.GroupsList = new SelectList(db.Groups.Where(x => x.IsDeleted == false).OrderBy(x => x.GroupName).Select(grp => new UserGroupsLocal { GroupId = grp.GroupID.ToString() + "~" + grp.OrganisationID.ToString(), GroupName = grp.GroupName }), "GroupId", "GroupName");
                ViewBag.OrganisationList = new SelectList(db.Organisations.Where(x => x.IsDeleted == false).OrderBy(x => x.OrganisationName).Select(org => org), "OrganisationID", "OrganisationName");
            }

            IEnumerable<Course> filterCourse = null;
            var currentLoginUser = Convert.ToInt64(Session["UserID"].ToString());
            var Groups = from g in db.UserGroups
                         where g.UserId == currentLoginUser
                         select g.GroupID;
            List<int> lst = Groups.ToList();
            var userrole = Session["UserRoles"].ToString().Contains("1");
            if (userrole)
            {
                filterCourse = from Cour in db.Courses
                               where Cour.IsDeleted == false && Cour.IsFinalized == true
                               orderby Cour.CourseName.ToLower()
                               select Cour;
            }
            else
            {

                if (currentuser.Organisation.IsUserAssignment)
                {
                    var liUserID = (from uf in db.UserProfiles where uf.OrganisationID == currentuser.OrganisationID && uf.IsDelete == false && uf.Status == true select uf.UserId).ToList();

                    filterCourse = ((from Cour in db.Courses
                                     join userCrs in db.UserCourses
                                     on Cour.CourseId equals userCrs.CourseId
                                     where Cour.IsDeleted == false && Cour.IsFinalized == true && userCrs.AssignedStatus == true && liUserID.Contains(userCrs.UserId)
                                     orderby Cour.CourseName.ToLower()
                                     select Cour).Union
                               (
                                from Crs in db.Courses
                                where Crs.IsDeleted == false && Crs.IsFinalized == true && Crs.CreatedById == currentLoginUser
                                orderby Crs.CourseName.ToLower()
                                select Crs
                               )).Distinct();

                }
                else
                {
                    filterCourse = ((from Cour in db.Courses
                                     join GrpCrs in db.GroupCourses
                                     on Cour.CourseId equals GrpCrs.CourseId
                                     where Cour.IsDeleted == false && Cour.IsFinalized == true && GrpCrs.AssignedStatus == true && lst.Contains(GrpCrs.GroupID)
                                     orderby Cour.CourseName.ToLower()
                                     select Cour).Union
                               (
                                from Crs in db.Courses
                                where Crs.IsDeleted == false && Crs.IsFinalized == true && Crs.CreatedById == currentLoginUser
                                orderby Crs.CourseName.ToLower()
                                select Crs
                               )).Distinct();
                }


            }
            ViewBag.CourseList = new SelectList(filterCourse.OrderBy(x => x.CourseName).Select(x => x), "CourseId", "CourseName");

            //var result = db.GetCourseReport(0, 0, 0, Convert.ToInt32(Session["UserID"])).ToList();
            //var res = from r in db.GetCourseReport(0, 0, 0, Convert.ToInt32(Session["UserID"]))
            //group r by new
            //{
            //    r.CourseName,
            //    r.TotalAssignment,
            //    r.InCompleted,
            //    r.NotStarted,
            //    r.Completed
            //} into g
            //select new
            //{
            //    g.Key.CourseName,
            //    TotalAssignment = g.Sum(x => x.TotalAssignment),
            //    InCompleted = g.Sum(x => x.InCompleted),
            //    NotStarted = g.Sum(x => x.NotStarted),
            //    Completed = g.Sum(x => x.Completed)
            //};

            var result = db.GetCourseReport(0, 0, 0, Convert.ToInt32(Session["UserID"]))
                .GroupBy(l => l.OrganisationName)
                .Select(cl => new GetCourseReport_Result
                {
                    OrganisationName = cl.First().OrganisationName,
                    TotalAssignment = cl.Max(c => c.TotalAssignment),
                    InCompleted = cl.Max(c => c.InCompleted),
                    NotStarted = cl.Max(c => c.NotStarted),
                    Completed = cl.Max(c => c.Completed),
                }).ToList();

            ViewBag.UserCourseData = result;

            //ViewBag.totalAssignment = result.GroupBy(x => x.CourseName, x => x.TotalAssignment);
            //ViewBag.totalInComplete = result.Select(x => x.InCompleted).Distinct().Count();
            //ViewBag.totalNotStarted = result.Select(x => x.NotStarted).Distinct().Count();
            //ViewBag.totalCompleted = result.Select(x => x.Completed).Distinct().Count();

            return View(usr);
        }

        [HttpPost]
        public ActionResult CourseReport(Rp_CourseReport Rp_Object, [System.Web.Http.FromBody]string ActionType)
        {
            if (Rp_Object.GroupId == null) Rp_Object.GroupId = 0;
            if (Rp_Object.OrganisationId == null) Rp_Object.OrganisationId = 0;
            if (Rp_Object.CourseId == null) Rp_Object.CourseId = 0;

            //var result = db.Rp_UserReport(Rp_Object.GroupId, Convert.ToInt32(Session["UserID"])).ToList();
            var result = db.GetCourseReport(Rp_Object.OrganisationId, Rp_Object.CourseId, Rp_Object.GroupId, Convert.ToInt32(Session["UserID"])).ToList();
            if (ActionType == "0")
                CourseReportExcel(result);
            else
                return CourseReportChartPdf(result);
            //RedirectToAction("CourseReportChart", "Reports", new {@Rp_Object = Rp_Object });
            //CourseReportChart(Rp_Object);

            bool IsGroupAdmin = (Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(2);
            var currentuser = db.UserProfiles.Find(Convert.ToInt64(Session["UserID"]));
            if (IsGroupAdmin)
            {
                var GroupAdminGroups = db.UserGroups.Where(x => x.UserId == currentuser.UserId).Select(x => x.GroupID).ToList();
                ViewBag.GroupsList = new SelectList(db.Groups.Where(x => x.IsDeleted == false && GroupAdminGroups.Contains(x.GroupID)).OrderBy(x => x.GroupName).Select(grp => new { GroupID = (Convert.ToString(grp.GroupID) + "~" + Convert.ToString(grp.OrganisationID)), GroupName = grp.GroupName }), "GroupID", "GroupName");
                ViewBag.OrganisationList = new SelectList(db.Organisations.Where(x => x.IsDeleted == false && x.OrganisationID == currentuser.OrganisationID).Select(org => org), "OrganisationID", "OrganisationName");
            }
            else
            {
                ViewBag.GroupsList = new SelectList(db.Groups.Where(x => x.IsDeleted == false).OrderBy(x => x.GroupName).Select(grp => new UserGroupsLocal { GroupId = grp.GroupID.ToString() + "~" + grp.OrganisationID.ToString(), GroupName = grp.GroupName }), "GroupId", "GroupName");
                ViewBag.OrganisationList = new SelectList(db.Organisations.Where(x => x.IsDeleted == false).OrderBy(x => x.OrganisationName).Select(org => org), "OrganisationID", "OrganisationName");
            }
            ViewBag.CourseList = new SelectList(db.Courses.Where(x => x.IsDeleted == false && x.IsFinalized == true).OrderBy(x => x.CourseName).Select(x => x), "CourseId", "CourseName");
            return View(Rp_Object);
        }

        public void CourseReportExcel(List<GetCourseReport_Result> result)
        {
            GridView gv = new GridView();
            if (result.Count() < 0)
            {
                gv.EmptyDataText = LMSResourse.Common.Common.lblBlankdata;
                gv.DataSource = null;
            }
            else
            {
                gv.DataSource = result;
                gv.DataBind();
            }
            Response.ClearContent();
            Response.Buffer = true;
            Response.AddHeader("content-disposition", "attachment; filename=Coursereport.xls");
            Response.ContentType = "application/ms-excel";
            Response.Charset = "";
            StringWriter sw = new StringWriter();
            HtmlTextWriter htw = new HtmlTextWriter(sw);
            gv.RenderControl(htw);
            Response.Output.Write(sw.ToString());
            Response.Flush();
            Response.End();
        }


        //    private Byte[] Chart()
        private Byte[] CourseChart(GetCourseReport_Result Obj)
        {

            var chart = new Chart
            {
                Width = 600,
                Height = 315,
                RenderType = RenderType.ImageTag,
                AntiAliasing = AntiAliasingStyles.All,
                TextAntiAliasingQuality = TextAntiAliasingQuality.High
            };



            chart.Titles.Add("");
            chart.ChartAreas.Add("");
            chart.ChartAreas[0].BackColor = System.Drawing.Color.White;
            chart.ChartAreas[0].BorderColor = System.Drawing.Color.Green;
            //chart.ChartAreas[0].Area3DStyle.Enable3D = true;

            chart.Series.Add("");
            chart.Series[0].ChartType = SeriesChartType.Pie;
            chart.Series[0]["PieLabelStyle"] = "Outside";
            chart.Series[0].BorderWidth = 1;
            chart.Series[0].BorderColor = System.Drawing.Color.Black;
            chart.Series[0].LegendText = "#VALX (#PERCENT)";

            // Set the pie label as well as legend text to be displayed as percentage
            // The P2 indicates a precision of 2 decimals
            chart.Series[0].Label = "#VALX";

            // By sorting the data points, they show up in proper ascending order in the legend
            chart.DataManipulator.Sort(PointSortOrder.Descending, chart.Series[0]);

            chart.Legends.Add("Legend1");
            chart.Legends[0].Enabled = true;
            chart.Legends[0].Docking = Docking.Right;
            chart.Legends[0].Alignment = System.Drawing.StringAlignment.Center;


            //double[] yearlySales = { 93, 100, 22, 23, 43, 54, 44, 9, 10, 1 };
            double[] ChartData = new double[] { Convert.ToInt32(Obj.NotStarted), Convert.ToInt32(Obj.InCompleted), Convert.ToInt32(Obj.Completed) };

            // Create a list of data
            //string[] salesPeopleTop10 = { "John Smith", "Patrick Johnson", "Michael Berube", "Paul Bradshaw", "Jacob Wright", "Jonathan Rosen", "Robert McDonald", "Joseph Hanson", "Marcel Thompson", "Trey Kelley" };
            string[] ChartDataWithLegend = new string[] { LMSResourse.Admin.Report.thNotStarted + " : " + Obj.NotStarted, LMSResourse.Admin.Report.thIncomplete + " : " + Obj.InCompleted, LMSResourse.Admin.Report.thCompleted + " : " + Obj.Completed };

            chart.Titles[0].Font = new System.Drawing.Font("Arial", 10f);

            System.Drawing.Color[] myPalette = new System.Drawing.Color[3];
            chart.Palette = ChartColorPalette.None;
            chart.PaletteCustomColors = myPalette;

            var chartPointsColoursFromDatabase = db.AppConstants.Where(x => x.ConstantID == 2).OrderBy(x => x.ConstantNameID).Select(x => x).ToList();

            int index = 0;
            int recordsInChart = 0;
            foreach (string chartDataPoints in ChartDataWithLegend)
            {
                if (ChartData[index] != 0)
                {
                    chart.Series[0].Points.AddXY(chartDataPoints, ChartData[index]);
                    switch (index)
                    {
                        case 0:
                            myPalette[recordsInChart] = System.Drawing.ColorTranslator.FromHtml(chartPointsColoursFromDatabase.Where(x => x.ConstantNameID == 1).Select(x => x.ConstantName).SingleOrDefault()); // System.Drawing.Color.FromKnownColor(KnownColor.Red);
                            break;
                        case 1:
                            myPalette[recordsInChart] = System.Drawing.ColorTranslator.FromHtml(chartPointsColoursFromDatabase.Where(x => x.ConstantNameID == 2).Select(x => x.ConstantName).SingleOrDefault()); //System.Drawing.Color.FromKnownColor(KnownColor.Violet);
                            break;
                        case 2:
                            myPalette[recordsInChart] = System.Drawing.ColorTranslator.FromHtml(chartPointsColoursFromDatabase.Where(x => x.ConstantNameID == 3).Select(x => x.ConstantName).SingleOrDefault());  //System.Drawing.Color.FromKnownColor(KnownColor.Green);
                            break;
                    }
                    recordsInChart++;
                }
                index++;
            }

            using (var chartimage = new MemoryStream())
            {
                chart.SaveImage(chartimage, ChartImageFormat.Png);
                return chartimage.GetBuffer();
            }
        }

        public FilePathResult CourseReportChartPdf(List<GetCourseReport_Result> result)
        {
            //if(Rp_Object==null) Rp_Object = new Rp_CourseReport();
            //if (Rp_Object.GroupId == null) Rp_Object.GroupId = 0;
            //if (Rp_Object.OrganisationId == null) Rp_Object.OrganisationId = 0;
            //if (Rp_Object.CourseId == null) Rp_Object.CourseId = 0;
            //var result = db.GetCourseReport(Rp_Object.OrganisationId, Rp_Object.CourseId, Rp_Object.GroupId, Convert.ToInt32(Session["UserID"])).ToList();

            var doc = new Document(PageSize.A4);
            string filename = (Session["UserID"]).ToString() + "_CourseReport" + ".pdf";

            var pdf = Server.MapPath("../Content/Charts/" + filename);

            PdfWriter.GetInstance(doc, new FileStream(pdf, FileMode.Create));
            doc.Open();

            var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
            var boldFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
            var boldFontWhite = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, iTextSharp.text.Color.WHITE);


            var normalFontHeading = FontFactory.GetFont(FontFactory.HELVETICA, 12);
            var boldFontHeading = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12);



            // doc.Add(ReportHeading);

            iTextSharp.text.Table docMainTable = new iTextSharp.text.Table(1, result.Count() * 6);

            //docMainTable.Alignment = Element.ALIGN_CENTER;

            docMainTable.DefaultVerticalAlignment = iTextSharp.text.Table.ALIGN_TOP;
            //docMainTable.DefaultHorizontalAlignment = iTextSharp.text.Table.ALIGN_CENTER;
            docMainTable.Alignment = iTextSharp.text.Table.ALIGN_TOP;
            docMainTable.TableFitsPage = true;
            docMainTable.Width = 100;


            docMainTable.BorderColor = iTextSharp.text.Color.WHITE;
            docMainTable.DefaultCell.Border = 0;

            int rowNumber = 0;
            //string organisationName = "";


            foreach (var z in result)
            {
                Cell ReportHeading;

                if (rowNumber == 0)
                    ReportHeading = new Cell(new Chunk(LMSResourse.Common.ApplicationMenu.tooltipCourseReport, boldFontHeading));
                else
                    ReportHeading = new Cell(new Chunk(LMSResourse.Common.ApplicationMenu.tooltipCourseReport, boldFontWhite));

                ReportHeading.HorizontalAlignment = Cell.ALIGN_CENTER;
                docMainTable.AddCell(ReportHeading);

                Cell OrganisationCell;
                OrganisationCell = new Cell(new Chunk("   " + LMSResourse.Admin.Report.thOrganisation + ": " + z.OrganisationName, normalFont));
                //OrganisationCell.BackgroundColor = iTextSharp.text.Color.ORANGE;

                docMainTable.AddCell(OrganisationCell);
                rowNumber++;
                //organisationName = z.OrganisationName;

                Cell CourseCell = new Cell(new Chunk("   " + LMSResourse.Admin.Report.thCourse + ": " + z.CourseName, normalFont));
                //CourseCell.BackgroundColor = iTextSharp.text.Color.CYAN;
                docMainTable.AddCell(CourseCell);
                rowNumber++;


                var image = iTextSharp.text.Image.GetInstance(CourseChart(z));
                //var image = iTextSharp.text.Image.GetInstance(Chart());
                image.ScalePercent(75f);
                docMainTable.AddCell(new Cell(image));
                rowNumber++;
                //doc.Add(image);

                iTextSharp.text.Table docTable = new iTextSharp.text.Table(3, 5);
                //docTable.Alignment = Element.ALIGN_CENTER;
                docTable.DefaultVerticalAlignment = Element.ALIGN_MIDDLE;
                //docTable.DefaultHorizontalAlignment = Element.ALIGN_CENTER;
                docTable.Cellspacing = 1;
                //docTable.DefaultCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                docTable.DefaultCell.Border = 0;
                docTable.AddCell(new Phrase(new Chunk(LMSResourse.Admin.Report.thStatus, boldFont)));
                docTable.AddCell(new Phrase(new Chunk(LMSResourse.Admin.Report.thNumber, boldFont)));
                docTable.AddCell(new Phrase(new Chunk(LMSResourse.Admin.Report.ThPercent, boldFont)));

                docTable.AddCell(new Phrase(new Chunk(LMSResourse.Admin.Report.thNotStarted, normalFont)));
                docTable.AddCell(new Phrase(new Chunk(z.NotStarted.ToString(), normalFont)));
                docTable.AddCell(new Phrase(new Chunk(String.Format("{0,5:N1}", Convert.ToDecimal(Convert.ToDecimal((decimal)z.NotStarted / (decimal)z.TotalAssignment)) * 100), normalFont)));

                docTable.AddCell(new Phrase(new Chunk(LMSResourse.Admin.Report.thIncomplete, normalFont)));
                docTable.AddCell(new Phrase(new Chunk(z.InCompleted.ToString(), normalFont)));
                docTable.AddCell(new Phrase(new Chunk(String.Format("{0,5:N1}", Convert.ToDecimal(Convert.ToDecimal((decimal)z.InCompleted / (decimal)z.TotalAssignment)) * 100), normalFont)));

                docTable.AddCell(new Phrase(new Chunk(LMSResourse.Admin.Report.thCompleted, normalFont)));
                docTable.AddCell(new Phrase(new Chunk(z.Completed.ToString(), normalFont)));
                docTable.AddCell(new Phrase(new Chunk(String.Format("{0,5:N1}", Convert.ToDecimal(Convert.ToDecimal((decimal)z.Completed / (decimal)z.TotalAssignment)) * 100), normalFont)));

                docTable.AddCell(new Phrase(new Chunk("Total", boldFont)));
                docTable.AddCell(new Phrase(new Chunk(z.TotalAssignment.ToString(), normalFont)));
                docTable.AddCell(new Phrase(new Chunk("", normalFont)));


                //doc.Add(docTable);
                docMainTable.AddCell(new Cell(docTable));
                rowNumber++;

                docMainTable.AddCell(new Cell(new Paragraph("\n")));


                //doc.Add(new Paragraph(""));
            }
            doc.Add(docMainTable);
            doc.Close();

            return File(pdf, "application/pdf", "Chart_CourseReport.pdf");
        }


        //public ActionResult CourseReportChart(Rp_CourseReport Rp_Object)
        //{
        //    if (Rp_Object.GroupId == null) Rp_Object.GroupId = 0;
        //    if (Rp_Object.OrganisationId == null) Rp_Object.OrganisationId = 0;
        //    if (Rp_Object.CourseId == null) Rp_Object.CourseId = 0;
        //    var result = db.GetCourseReport(Rp_Object.OrganisationId, Rp_Object.CourseId, Rp_Object.GroupId, Convert.ToInt32(Session["UserID"])).ToList();
        //    //var result = new CLSLms.GetCourseReport_Result[] { new GetCourseReport_Result { CourseName = "abc", Completed = 2, InCompleted = 2, NotStarted = 1, TotalAssignment = 0, CategoryName = "abc" }, new GetCourseReport_Result { CourseName = "abc", Completed = 2, InCompleted = 2, NotStarted = 1, TotalAssignment = 0, CategoryName = "abc" }, new GetCourseReport_Result { CourseName = "abc", Completed = 2, InCompleted = 2, NotStarted = 1, TotalAssignment = 0, CategoryName = "abc" } };
        //    return View(result);
        //}

        public ActionResult AjaxHandlerCourseReport(jQueryDataTableParamModel param)
        {
            var Params = param.fCol1.Split('~');

            var returnResult = (from obj in db.GetUserCourseReport(Convert.ToInt32(Params[0]), 0, Convert.ToInt32(Params[2]), Convert.ToInt32(Session["UserID"]), false, 0, "0")
                                group obj by new
                                {
                                    obj.CourseName,
                                    obj.IsQuiz
                                }
                         into gs
                                select new CourseReport
                                {
                                    CourseName = gs.Key.CourseName,
                                    isQuiz = gs.Key.IsQuiz,
                                    AssignedUsers = gs.Count(c => c.EmailAddress != null),
                                    Completed = gs.Count(c => c.Status == "Completed"),
                                    NotStarted = gs.Count(c => c.Status == "Not started"),
                                    InProgress = gs.Count(c => c.Status == "In progress")

                                }).ToList();

            // records to display            
            var displayedRecord = returnResult.Skip(param.iDisplayStart).Take(param.iDisplayLength);
            if (param.iDisplayLength == -1)
                displayedRecord = returnResult;

            var result = from obj in displayedRecord
                         select new[] {
                              obj.CourseName,
                              (obj.isQuiz==0)?db.Courses.Where(c=>c.CourseName==obj.CourseName).Select(S=>S.Category.CategoryName).FirstOrDefault():
                              db.Quizes.Where(c=>c.QuizName==obj.CourseName).Select(S=>S.Category.CategoryName).FirstOrDefault(),
                              obj.AssignedUsers.ToString(),
                              obj.Completed.ToString(),
                              obj.InProgress.ToString(),
                              obj.NotStarted.ToString(),
                              (obj.isQuiz==0)?db.Courses.Where(c=>c.CourseName==obj.CourseName).Select(S=>S.CourseId).FirstOrDefault().ToString():
                              db.Quizes.Where(c=>c.QuizName==obj.CourseName).Select(S=>S.QuizID).FirstOrDefault().ToString(),
                              obj.isQuiz.ToString()

                          };

            return Json(new
            {
                sEcho = param.sEcho,
                iTotalRecords = returnResult.Count(),
                iTotalDisplayRecords = returnResult.Count(),
                aaData = result
            },
                           JsonRequestBehavior.AllowGet);
        }
        #endregion

        #region // Organization License details report

        public ActionResult OrganisationLicenceReport()
        {
            Rp_UserReport usr = new Rp_UserReport();
            usr.GroupId = 0;
            bool IsGroupAdmin = (Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(2);
            var currentuser = db.UserProfiles.Find(Convert.ToInt64(Session["UserID"]));
            if (IsGroupAdmin)
            {
                var GroupAdminGroups = db.UserGroups.Where(x => x.UserId == currentuser.UserId).Select(x => x.GroupID).ToList();
                ViewBag.GroupsList = new SelectList(db.Groups.Where(x => x.IsDeleted == false && GroupAdminGroups.Contains(x.GroupID)).OrderBy(x => x.GroupName).Select(grp => new UserGroupsLocal { GroupId = grp.GroupID.ToString() + "~" + grp.OrganisationID.ToString(), GroupName = grp.GroupName }), "GroupId", "GroupName");
                ViewBag.OrganisationList = new SelectList(db.Organisations.Where(x => x.IsDeleted == false && x.OrganisationID == currentuser.OrganisationID).Select(org => org), "OrganisationID", "OrganisationName");
            }
            else
            {
                ViewBag.GroupsList = new SelectList(db.Groups.Where(x => x.IsDeleted == false).OrderBy(x => x.GroupName).Select(grp => new UserGroupsLocal { GroupId = grp.GroupID.ToString() + "~" + grp.OrganisationID.ToString(), GroupName = grp.GroupName }), "GroupId", "GroupName");
                ViewBag.OrganisationList = new SelectList(db.Organisations.Where(x => x.IsDeleted == false).OrderBy(x => x.OrganisationName).Select(org => org), "OrganisationID", "OrganisationName");
            }
            IEnumerable<Course> filterCourse = null;
            var currentLoginUser = Convert.ToInt64(Session["UserID"].ToString());

            var Groups = from g in db.UserGroups
                         where g.UserId == currentLoginUser
                         select g.GroupID;
            List<int> lst = Groups.ToList();
            var userrole = Session["UserRoles"].ToString().Contains("1");
            if (userrole)
            {
                filterCourse = from Cour in db.Courses
                               where Cour.IsDeleted == false && Cour.IsFinalized == true
                               orderby Cour.CourseName.ToLower()
                               select Cour;
            }
            else
            {
                if (currentuser.Organisation.IsUserAssignment)
                {
                    var liUserID = (from uf in db.UserProfiles where uf.OrganisationID == currentuser.OrganisationID && uf.IsDelete == false && uf.Status == true select uf.UserId).ToList();

                    filterCourse = ((from Cour in db.Courses
                                     join userCrs in db.UserCourses
                                     on Cour.CourseId equals userCrs.CourseId
                                     where Cour.IsDeleted == false && Cour.IsFinalized == true && userCrs.AssignedStatus == true && liUserID.Contains(userCrs.UserId)
                                     orderby Cour.CourseName.ToLower()
                                     select Cour).Union
                               (
                                from Crs in db.Courses
                                where Crs.IsDeleted == false && Crs.IsFinalized == true && Crs.CreatedById == currentLoginUser
                                orderby Crs.CourseName.ToLower()
                                select Crs
                               )).Distinct();

                }
                else
                {
                    filterCourse = ((from Cour in db.Courses
                                     join GrpCrs in db.GroupCourses
                                     on Cour.CourseId equals GrpCrs.CourseId
                                     where Cour.IsDeleted == false && Cour.IsFinalized == true && GrpCrs.AssignedStatus == true && lst.Contains(GrpCrs.GroupID)
                                     orderby Cour.CourseName.ToLower()
                                     select Cour).Union
                                   (
                                    from Crs in db.Courses
                                    where Crs.IsDeleted == false && Crs.IsFinalized == true && Crs.CreatedById == currentLoginUser
                                    orderby Crs.CourseName.ToLower()
                                    select Crs
                                   )).Distinct();
                }
            }
            ViewBag.CourseList = new SelectList(filterCourse.OrderBy(x => x.CourseName).Select(x => x), "CourseId", "CourseName");
            return View();
        }

        public ActionResult AjaxHandlerOrganisationLicenceReport(jQueryDataTableParamModel param)
        {
            var Params = param.fCol1.Split('~');
            var returnResult = db.GetOrganizationLicenseReport(Convert.ToInt32(Params[0]), Convert.ToInt32(Params[1]), Convert.ToInt32(Params[2]), Convert.ToInt32(Session["UserID"])).ToList();


            // records to display            
            var displayedRecord = returnResult.Skip(param.iDisplayStart).Take(param.iDisplayLength);
            if (param.iDisplayLength == -1)
                displayedRecord = returnResult;

            var result = from obj in displayedRecord
                         select new[] {
                              obj.OrganisationName,
                              obj.GroupName,
                              Convert.ToString(obj.MaxUsers),
                              Convert.ToString(obj.AssignedUsers),
                              obj.CourseName,
                              obj.FirstName,
                              obj.LastName,
                              obj.EmailAddress,
                              !String.IsNullOrEmpty(obj.CourseExpiryDate)?obj.CourseExpiryDate:"",
                              obj.Status,
                              obj.Completiondate.HasValue?string.Format("{0:dd/MM/yyyy HH:mm}",obj.Completiondate):"",
                              obj.Score.ToString()
                          };

            return Json(new
            {
                sEcho = param.sEcho,
                iTotalRecords = returnResult.Count(),
                iTotalDisplayRecords = returnResult.Count(),
                aaData = result
            },
                           JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult OrganisationLicenceReport(Rp_UserCourseReport Rp_Object)
        {
            if (Rp_Object.GroupId == null) Rp_Object.GroupId = 0;
            if (Rp_Object.OrganisationId == null) Rp_Object.OrganisationId = 0;
            if (Rp_Object.CourseId == null) Rp_Object.CourseId = 0;

            //var result = db.Rp_UserReport(Rp_Object.GroupId, Convert.ToInt32(Session["UserID"])).ToList();
            var result = db.GetOrganizationLicenseReport(Rp_Object.OrganisationId, Rp_Object.CourseId, Rp_Object.GroupId, Convert.ToInt32(Session["UserID"])).ToList();
            OrganisationLicenceReportExcel(result);


            bool IsGroupAdmin = (Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(2);
            var currentuser = db.UserProfiles.Find(Convert.ToInt64(Session["UserID"]));
            if (IsGroupAdmin)
            {
                var GroupAdminGroups = db.UserGroups.Where(x => x.UserId == currentuser.UserId).Select(x => x.GroupID).ToList();
                ViewBag.GroupsList = new SelectList(db.Groups.Where(x => x.IsDeleted == false && GroupAdminGroups.Contains(x.GroupID)).OrderBy(x => x.GroupName).Select(grp => new { GroupID = (Convert.ToString(grp.GroupID) + "~" + Convert.ToString(grp.OrganisationID)), GroupName = grp.GroupName }), "GroupID", "GroupName");
                ViewBag.OrganisationList = new SelectList(db.Organisations.Where(x => x.IsDeleted == false && x.OrganisationID == currentuser.OrganisationID).Select(org => org), "OrganisationID", "OrganisationName");
            }
            else
            {
                ViewBag.GroupsList = new SelectList(db.Groups.Where(x => x.IsDeleted == false).OrderBy(x => x.GroupName).Select(grp => new UserGroupsLocal { GroupId = grp.GroupID.ToString() + "~" + grp.OrganisationID.ToString(), GroupName = grp.GroupName }), "GroupId", "GroupName");
                ViewBag.OrganisationList = new SelectList(db.Organisations.Where(x => x.IsDeleted == false).OrderBy(x => x.OrganisationName).Select(org => org), "OrganisationID", "OrganisationName");
            }
            ViewBag.CourseList = new SelectList(db.Courses.Where(x => x.IsDeleted == false && x.IsFinalized == true).OrderBy(x => x.CourseName).Select(x => x), "CourseId", "CourseName");
            return View(Rp_Object);
        }

        public void OrganisationLicenceReportExcel(List<GetOrganizationLicenseReport_Result> result)
        {
            GridView gv = new GridView();
            if (result.Count() < 0)
            {
                gv.EmptyDataText = LMSResourse.Common.Common.lblBlankdata;
                gv.DataSource = null;
            }
            else
            {
                gv.DataSource = result;
                gv.DataBind();
            }
            Response.ClearContent();
            Response.Buffer = true;
            Response.AddHeader("content-disposition", "attachment; filename=OrganizationLicenseReport.xls");
            Response.ContentType = "application/ms-excel";
            Response.Charset = "";
            StringWriter sw = new StringWriter();
            HtmlTextWriter htw = new HtmlTextWriter(sw);
            gv.RenderControl(htw);
            Response.Output.Write(sw.ToString());
            Response.Flush();
            Response.End();
        }

        public ActionResult GetCoursesForGroupByID(string gid)
        {
            long id = Convert.ToInt64(gid);
            string str = "<option value=''>All</option>";

            if (id > 0)
            {
                var isUserAssign = db.Groups.Where(a => a.GroupID == id).FirstOrDefault();
                if (isUserAssign != null && isUserAssign.Organisation != null && !isUserAssign.Organisation.IsUserAssignment)
                {
                    var filterCourse = db.GroupCourses.Where(a => a.Course.IsDeleted == false && a.Course.IsFinalized == true && a.AssignedStatus == true && a.GroupID == id).OrderBy(b => b.Course.CourseName).ToList();
                    foreach (var cour in filterCourse)
                    {
                        str += "<option value='" + cour.CourseId + "'>" + cour.Course.CourseName + "</option>";
                    }
                }
                else
                {
                    var filterCourse = db.Courses.Where(a => a.IsDeleted == false && a.IsFinalized == true).OrderBy(b => b.CourseName).ToList();
                    foreach (var cour in filterCourse)
                    {
                        str += "<option value='" + cour.CourseId + "'>" + cour.CourseName + "</option>";
                    }
                }
            }
            else
            {
                var filterCourse = db.Courses.Where(a => a.IsDeleted == false && a.IsFinalized == true).OrderBy(b => b.CourseName).ToList();
                foreach (var cour in filterCourse)
                {
                    str += "<option value='" + cour.CourseId + "'>" + cour.CourseName + "</option>";
                }
            }

            return Json(new { data = str }, JsonRequestBehavior.AllowGet);

        }

        public ActionResult CheckIsUserindividualAssigned(string Oid)
        {
            var id = Convert.ToInt64(Oid);
            var orgDetail = db.Organisations.Where(a => a.OrganisationID == id).FirstOrDefault();
            return Json(new { data = (orgDetail != null ? orgDetail.IsUserAssignment : false) }, JsonRequestBehavior.AllowGet);

        }

        #endregion

        #region // User Course Archive report
        public ActionResult UserCourseArchiveReport()
        {
            Rp_UserReport usr = new Rp_UserReport();
            usr.GroupId = 0;
            bool IsGroupAdmin = (Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(2);
            var currentuser = db.UserProfiles.Find(Convert.ToInt64(Session["UserID"]));
            if (IsGroupAdmin)
            {
                var GroupAdminGroups = db.UserGroups.Where(x => x.UserId == currentuser.UserId).Select(x => x.GroupID).ToList();
                ViewBag.GroupsList = new SelectList(db.Groups.Where(x => x.IsDeleted == false && GroupAdminGroups.Contains(x.GroupID)).OrderBy(x => x.GroupName).Select(grp => new UserGroupsLocal { GroupId = grp.GroupID.ToString() + "~" + grp.OrganisationID.ToString(), GroupName = grp.GroupName }), "GroupId", "GroupName");

                IEnumerable<UserProfile> UserList = null;
                UserList = ((from usrpro in db.UserProfiles
                             join usrgrp in db.UserGroups
                             on usrpro.UserId equals usrgrp.UserId
                             where GroupAdminGroups.Contains(usrgrp.GroupID)
                             select usrpro));


                ViewBag.UsersList = new SelectList(UserList.Where(x => x.IsDelete == false).OrderBy(u => u.FirstName).OrderBy(u => u.LastName).Select(u => new clsUserFullName { UserId = u.UserId, UserFullName = u.FirstName + ' ' + u.LastName }), "UserId", "UserFullName");
                ViewBag.OrganisationList = new SelectList(db.Organisations.Where(x => x.IsDeleted == false && x.OrganisationID == currentuser.OrganisationID).Select(org => org), "OrganisationID", "OrganisationName");
            }
            else
            {
                ViewBag.GroupsList = new SelectList(db.Groups.Where(x => x.IsDeleted == false).OrderBy(x => x.GroupName).Select(grp => new UserGroupsLocal { GroupId = grp.GroupID.ToString() + "~" + grp.OrganisationID.ToString(), GroupName = grp.GroupName }), "GroupId", "GroupName");
                ViewBag.OrganisationList = new SelectList(db.Organisations.Where(x => x.IsDeleted == false).OrderBy(x => x.OrganisationName).Select(org => org), "OrganisationID", "OrganisationName");
                IEnumerable<UserProfile> UserList1 = null;
                UserList1 = ((from usrpro in db.UserProfiles
                              select usrpro));
                ViewBag.UsersList = new SelectList(UserList1.Where(x => x.IsDelete == false).OrderBy(u => u.FirstName).OrderBy(u => u.LastName).Select(u => new clsUserFullName { UserId = u.UserId, UserFullName = u.FirstName + ' ' + u.LastName }), "UserId", "UserFullName");
            }
            IEnumerable<Course> filterCourse = null;
            var currentLoginUser = Convert.ToInt64(Session["UserID"].ToString());

            var Groups = from g in db.UserGroups
                         where g.UserId == currentLoginUser
                         select g.GroupID;
            List<int> lst = Groups.ToList();
            var userrole = Session["UserRoles"].ToString().Contains("1");
            if (userrole)
            {
                filterCourse = from Cour in db.Courses
                               where Cour.IsDeleted == false && Cour.IsFinalized == true
                               orderby Cour.CourseName.ToLower()
                               select Cour;
            }
            else
            {
                if (currentuser.Organisation.IsUserAssignment)
                {
                    var liUserID = (from uf in db.UserProfiles where uf.OrganisationID == currentuser.OrganisationID && uf.IsDelete == false && uf.Status == true select uf.UserId).ToList();

                    filterCourse = ((from Cour in db.Courses
                                     join userCrs in db.UserCourses
                                     on Cour.CourseId equals userCrs.CourseId
                                     where Cour.IsDeleted == false && Cour.IsFinalized == true && userCrs.AssignedStatus == true && liUserID.Contains(userCrs.UserId)
                                     orderby Cour.CourseName.ToLower()
                                     select Cour).Union
                               (
                                from Crs in db.Courses
                                where Crs.IsDeleted == false && Crs.IsFinalized == true && Crs.CreatedById == currentLoginUser
                                orderby Crs.CourseName.ToLower()
                                select Crs
                               )).Distinct();

                }
                else
                {
                    filterCourse = ((from Cour in db.Courses
                                     join GrpCrs in db.GroupCourses
                                     on Cour.CourseId equals GrpCrs.CourseId
                                     where Cour.IsDeleted == false && Cour.IsFinalized == true && GrpCrs.AssignedStatus == true && lst.Contains(GrpCrs.GroupID)
                                     orderby Cour.CourseName.ToLower()
                                     select Cour).Union
                               (
                                from Crs in db.Courses
                                where Crs.IsDeleted == false && Crs.IsFinalized == true && Crs.CreatedById == currentLoginUser
                                orderby Crs.CourseName.ToLower()
                                select Crs
                               )).Distinct();
                }

            }
            ViewBag.CourseList = new SelectList(filterCourse.OrderBy(x => x.CourseName).Select(x => x), "CourseId", "CourseName");
            return View();
        }

        [HttpPost]
        public ActionResult UserCourseArchiveReport(Rp_UserCourseReport Rp_Object)
        {
            if (Rp_Object.GroupId == null) Rp_Object.GroupId = 0;
            if (Rp_Object.OrganisationId == null) Rp_Object.OrganisationId = 0;
            if (Rp_Object.CourseId == null) Rp_Object.CourseId = 0;
            if (Rp_Object.SelectedUserId == null) Rp_Object.SelectedUserId = 0;

            //var result = db.Rp_UserReport(Rp_Object.GroupId, Convert.ToInt32(Session["UserID"])).ToList();
            var result = db.GetUserCourseArchiveReport(Rp_Object.OrganisationId, Rp_Object.CourseId, Rp_Object.GroupId, Rp_Object.SelectedUserId, Convert.ToInt32(Session["UserID"])).ToList();
            UserCourseArchiveReportExcel(result);


            bool IsGroupAdmin = (Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(2);
            var currentuser = db.UserProfiles.Find(Convert.ToInt64(Session["UserID"]));
            if (IsGroupAdmin)
            {
                var GroupAdminGroups = db.UserGroups.Where(x => x.UserId == currentuser.UserId).Select(x => x.GroupID).ToList();
                ViewBag.GroupsList = new SelectList(db.Groups.Where(x => x.IsDeleted == false && GroupAdminGroups.Contains(x.GroupID)).OrderBy(x => x.GroupName).Select(grp => new { GroupID = (Convert.ToString(grp.GroupID) + "~" + Convert.ToString(grp.OrganisationID)), GroupName = grp.GroupName }), "GroupID", "GroupName");
                IEnumerable<UserProfile> UserList = null;
                UserList = ((from usrpro in db.UserProfiles
                             join usrgrp in db.UserGroups
                             on usrpro.UserId equals usrgrp.UserId
                             where GroupAdminGroups.Contains(usrgrp.GroupID)
                             select usrpro));

                ViewBag.UsersList = new SelectList(UserList.Where(x => x.IsDelete == false).OrderBy(u => u.FirstName).OrderBy(u => u.LastName).Select(u => new clsUserFullName { UserId = u.UserId, UserFullName = u.FirstName + ' ' + u.LastName }), "UserId", "UserFullName");
                ViewBag.OrganisationList = new SelectList(db.Organisations.Where(x => x.IsDeleted == false && x.OrganisationID == currentuser.OrganisationID).Select(org => org), "OrganisationID", "OrganisationName");
            }
            else
            {
                ViewBag.GroupsList = new SelectList(db.Groups.Where(x => x.IsDeleted == false).OrderBy(x => x.GroupName).Select(grp => new UserGroupsLocal { GroupId = grp.GroupID.ToString() + "~" + grp.OrganisationID.ToString(), GroupName = grp.GroupName }), "GroupId", "GroupName");
                ViewBag.OrganisationList = new SelectList(db.Organisations.Where(x => x.IsDeleted == false).OrderBy(x => x.OrganisationName).Select(org => org), "OrganisationID", "OrganisationName");
                IEnumerable<UserProfile> UserList1 = null;
                UserList1 = ((from usrpro in db.UserProfiles
                              select usrpro));
                ViewBag.UsersList = new SelectList(UserList1.Where(x => x.IsDelete == false).OrderBy(u => u.FirstName).OrderBy(u => u.LastName).Select(u => new clsUserFullName { UserId = u.UserId, UserFullName = u.FirstName + ' ' + u.LastName }), "UserId", "UserFullName");
            }
            ViewBag.CourseList = new SelectList(db.Courses.Where(x => x.IsDeleted == false && x.IsFinalized == true).OrderBy(x => x.CourseName).Select(x => x), "CourseId", "CourseName");
            return View(Rp_Object);
        }

        public ActionResult GetUsersForOrgByID(string Orgid)
        {
            int id = Convert.ToInt32(Orgid);
            string str = "<option value=''>All</option>";

            if (id > 0)
            {
                var filterUser = db.UserProfiles.Where(u => u.IsDelete == false && u.OrganisationID == id).OrderBy(u => u.FirstName).ToList();
                foreach (var usr in filterUser)
                {
                    str += "<option value='" + usr.UserId + "'>" + usr.FirstName + ' ' + usr.LastName + "</option>";
                }
            }
            else
            {
                var filterUser = db.UserProfiles.Where(u => u.IsDelete == false).OrderBy(u => u.FirstName).ToList();
                foreach (var usr in filterUser)
                {
                    str += "<option value='" + usr.UserId + "'>" + usr.FirstName + ' ' + usr.LastName + "</option>";
                }
            }

            return Json(new { data = str }, JsonRequestBehavior.AllowGet);

        }

        public void UserCourseArchiveReportExcel(List<GetUserCourseArchiveReport_Result> result)
        {
            GridView gv = new GridView();
            if (result.Count() < 0)
            {
                gv.EmptyDataText = LMSResourse.Common.Common.lblBlankdata;
                gv.DataSource = null;
            }
            else
            {
                gv.DataSource = result;
                gv.DataBind();
            }
            Response.ClearContent();
            Response.Buffer = true;
            Response.AddHeader("content-disposition", "attachment; filename=UserCourseArchivereport.xls");
            Response.ContentType = "application/ms-excel";
            Response.Charset = "";
            StringWriter sw = new StringWriter();
            HtmlTextWriter htw = new HtmlTextWriter(sw);
            gv.RenderControl(htw);
            Response.Output.Write(sw.ToString());
            Response.Flush();
            Response.End();
        }

        public ActionResult AjaxHandlerUserCourseArchiveReport1(jQueryDataTableParamModel param)
        {
            var Params = param.fCol1.Split('~');
            var returnResult = db.GetUserCourseArchiveReport(Convert.ToInt32(Params[0]), Convert.ToInt32(Params[1]), Convert.ToInt32(Params[2]), Convert.ToInt32(Params[3]), Convert.ToInt32(Session["UserID"])).ToList();


            // records to display            
            var displayedRecord = returnResult.Skip(param.iDisplayStart).Take(param.iDisplayLength);
            if (param.iDisplayLength == -1)
                displayedRecord = returnResult;

            var result = from obj in displayedRecord
                         select new[] {
                              obj.FirstName,
                              obj.LastName,
                              obj.EmailAddress,
                              obj.OrganisationName,
                              obj.CourseName,
                              obj.Status,
                              obj.Completiondate.HasValue?string.Format("{0:dd/MM/yyyy HH:mm}",obj.Completiondate):"",
                              obj.Score.ToString(),
                              string.Format("{0:dd/MM/yyyy}",obj.ExpiryDate)
                          };

            return Json(new
            {
                sEcho = param.sEcho,
                iTotalRecords = returnResult.Count(),
                iTotalDisplayRecords = returnResult.Count(),
                aaData = result
            },
            JsonRequestBehavior.AllowGet);
        }
        #endregion

        #region // Organization User drill down report

        public ActionResult OrganisationUserReport()
        {
            var result = db.GetOrganisationUserReport().ToList<GetOrganisationUserReport_Result>();
            ViewBag.ouReport = result;

            return View();
        }
        #endregion

        public ActionResult AjaxHandlerGetGroup(int OrgId)
        {
            //List<Group> Obj = new List<Group>();
            bool IsGroupAdmin = (Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(2);
            db.Configuration.ProxyCreationEnabled = false;
            var currentuser = db.UserProfiles.Find(Convert.ToInt64(Session["UserID"]));
            if (IsGroupAdmin)
            {
                var GroupAdminGroups = db.UserGroups.Where(x => x.UserId == currentuser.UserId).Select(x => x.GroupID).ToList();
                var Obj = db.Groups.Where(x => x.IsDeleted == false && GroupAdminGroups.Contains(x.GroupID) && x.OrganisationID == OrgId).OrderBy(x => x.GroupName);
                return Json(Obj, JsonRequestBehavior.AllowGet);
            }
            else
            {
                var Obj = db.Groups.Where(x => x.IsDeleted == false && x.OrganisationID == OrgId).OrderBy(x => x.GroupName);
                return Json(Obj, JsonRequestBehavior.AllowGet);
            }
        }

        #region // User Quiz report
        public ActionResult UserQuizReport()
        {
            Rp_UserReport usr = new Rp_UserReport();
            usr.GroupId = 0;
            bool IsGroupAdmin = (Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(2);
            var currentuser = db.UserProfiles.Find(Convert.ToInt64(Session["UserID"]));
            if (IsGroupAdmin)
            {
                var GroupAdminGroups = db.UserGroups.Where(x => x.UserId == currentuser.UserId).Select(x => x.GroupID).ToList();
                ViewBag.GroupsList = new SelectList(db.Groups.Where(x => x.IsDeleted == false && GroupAdminGroups.Contains(x.GroupID)).OrderBy(x => x.GroupName).Select(grp => new UserGroupsLocal { GroupId = grp.GroupID.ToString() + "~" + grp.OrganisationID.ToString(), GroupName = grp.GroupName }), "GroupId", "GroupName");
                //ViewBag.OrganisationList = new SelectList(db.Organisations.Where(x => x.IsDeleted == false && x.OrganisationID == currentuser.OrganisationID).Select(org => org), "OrganisationID", "OrganisationName");
            }
            else
            {
                ViewBag.GroupsList = new SelectList(db.Groups.Where(x => x.IsDeleted == false).OrderBy(x => x.GroupName).Select(grp => new UserGroupsLocal { GroupId = grp.GroupID.ToString() + "~" + grp.OrganisationID.ToString(), GroupName = grp.GroupName }), "GroupId", "GroupName");
                //ViewBag.OrganisationList = new SelectList(db.Organisations.Where(x => x.IsDeleted == false).OrderBy(x => x.OrganisationName).Select(org => org), "OrganisationID", "OrganisationName");
            }
            IEnumerable<Quize> filterQuiz = null;
            var currentLoginUser = Convert.ToInt64(Session["UserID"].ToString());

            var Groups = from g in db.UserGroups
                         where g.UserId == currentLoginUser
                         select g.GroupID;
            List<int> lst = Groups.ToList();
            var userrole = Session["UserRoles"].ToString().Contains("1");
            if (userrole)
            {
                filterQuiz = from qz in db.Quizes
                                 //where Cour.IsDeleted == false && Cour.IsFinalized == true
                             orderby qz.QuizName.ToLower()
                             select qz;
            }
            else
            {
                if (currentuser.Organisation.IsUserAssignment)
                {
                    var liUserID = (from uf in db.UserProfiles where uf.OrganisationID == currentuser.OrganisationID && uf.IsDelete == false && uf.Status == true select uf.UserId).ToList();

                    filterQuiz = ((from qz in db.Quizes
                                   join userCrs in db.UserQuizs
                                   on qz.QuizID equals userCrs.QuizID
                                   where //Cour.IsDeleted == false && Cour.IsFinalized == true && 
                                   userCrs.AssignedStatus == true && liUserID.Contains(userCrs.UserId)
                                   orderby qz.QuizName.ToLower()
                                   select qz).Union
                               (
                                from qzs in db.Quizes
                                where //Crs.IsDeleted == false && Crs.IsFinalized == true && 
                                qzs.CreatedAdminID == currentLoginUser
                                orderby qzs.QuizName.ToLower()
                                select qzs
                               )).Distinct();

                }
                else
                {
                    filterQuiz = ((from qz in db.Quizes
                                   join GrpCrs in db.GroupQuizs
                                     on qz.QuizID equals GrpCrs.QuizID
                                   where //Cour.IsDeleted == false && Cour.IsFinalized == true && 
                                   GrpCrs.AssignedStatus == true && lst.Contains(GrpCrs.GroupID)
                                   orderby qz.QuizName.ToLower()
                                   select qz).Union
                               (
                                from qzs in db.Quizes
                                where //Crs.IsDeleted == false && Crs.IsFinalized == true && 
                                qzs.CreatedAdminID == currentLoginUser
                                orderby qzs.QuizName.ToLower()
                                select qzs
                               )).Distinct();
                }

            }

            int?[] totalUsers = new int?[12];
            decimal?[] totalPassedUsers = new decimal?[12];
            int?[] totalRegisteredUsers = new int?[12];


            for (int i = 1; i <= 12; i++)
            {
                var userCoursePassPercentage = db.GetUserCoursePassPercentageMonthwise(i, true).FirstOrDefault();
                totalUsers[i - 1] = userCoursePassPercentage.TotalUsers;
                totalPassedUsers[i - 1] = userCoursePassPercentage.TotalUserCoursePassPercentage;
                totalRegisteredUsers[i - 1] = userCoursePassPercentage.TotalRegisteredUsers;
            }

            ViewBag.TotalUsers = totalUsers;
            ViewBag.TotalPassedUsers = totalPassedUsers;
            ViewBag.TotalRegisteredUsers = totalRegisteredUsers;
            ViewBag.CategoryList = new SelectList(db.Categories.OrderBy(x => x.CategoryName).Select(x => x), "CategoryId", "CategoryName");
            ViewBag.QuizList = new SelectList(filterQuiz.OrderBy(x => x.QuizName).Select(x => x), "QuizID", "QuizName");
            return View();
        }

        [HttpPost]
        public ActionResult UserQuizReport(Rp_UserQuizReport Rp_Object)
        {
            if (Rp_Object.GroupId == null) Rp_Object.GroupId = 0;
            if (Rp_Object.CategoryId == null) Rp_Object.CategoryId = 0;
            if (Rp_Object.QuizID == null) Rp_Object.QuizID = 0;

            var result = db.GetUserQuizReport(Rp_Object.CategoryId, Rp_Object.QuizID, Rp_Object.GroupId, Convert.ToInt32(Session["UserID"]), false).ToList();
            UserQuizReportExcel(result);

            bool IsGroupAdmin = (Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(2);
            var currentuser = db.UserProfiles.Find(Convert.ToInt64(Session["UserID"]));
            if (IsGroupAdmin)
            {
                var GroupAdminGroups = db.UserGroups.Where(x => x.UserId == currentuser.UserId).Select(x => x.GroupID).ToList();
                ViewBag.GroupsList = new SelectList(db.Groups.Where(x => x.IsDeleted == false && GroupAdminGroups.Contains(x.GroupID)).OrderBy(x => x.GroupName).Select(grp => new { GroupID = (Convert.ToString(grp.GroupID) + "~" + Convert.ToString(grp.OrganisationID)), GroupName = grp.GroupName }), "GroupID", "GroupName");
            }
            else
            {
                ViewBag.GroupsList = new SelectList(db.Groups.Where(x => x.IsDeleted == false).OrderBy(x => x.GroupName).Select(grp => new UserGroupsLocal { GroupId = grp.GroupID.ToString() + "~" + grp.OrganisationID.ToString(), GroupName = grp.GroupName }), "GroupId", "GroupName");
            }

            ViewBag.CategoryList = new SelectList(db.Categories.OrderBy(x => x.CategoryName).Select(x => x), "CategoryId", "CategoryName");
            ViewBag.QuizList = new SelectList(db.Quizes.OrderBy(x => x.QuizName).Select(x => x), "QuizID", "QuizName");
            return View(Rp_Object);
        }

        public void UserQuizReportExcel(List<GetUserQuizReport_Result> result)
        {
            GridView gv = new GridView();
            if (result.Count() < 0)
            {
                gv.EmptyDataText = LMSResourse.Common.Common.lblBlankdata;
                gv.DataSource = null;
            }
            else
            {
                gv.DataSource = result;
                gv.DataBind();
                gv.HeaderRow.Cells[0].Visible = false;
                gv.HeaderRow.Cells[1].Visible = false;

                for (int i = 0; i < gv.Rows.Count; i++)
                {
                    gv.Rows[i].Cells[0].Visible = false;
                    gv.Rows[i].Cells[1].Visible = false;
                }
            }
            Response.ClearContent();
            Response.Buffer = true;
            Response.AddHeader("content-disposition", "attachment; filename=UserCoursereport.xls");
            Response.ContentType = "application/ms-excel";
            Response.Charset = "";
            StringWriter sw = new StringWriter();
            HtmlTextWriter htw = new HtmlTextWriter(sw);
            gv.RenderControl(htw);
            Response.Output.Write(sw.ToString());
            Response.Flush();
            Response.End();
        }

        public ActionResult AjaxHandlerUserQuizReport(jQueryDataTableParamModel param)
        {
            var Params = param.fCol1.Split('~');
            var returnResult = db.GetUserQuizReport(Convert.ToInt32(Params[0]), Convert.ToInt32(Params[1]), Convert.ToInt32(Params[2]), Convert.ToInt32(Session["UserID"]), false).ToList();

            var displayedRecord = from obj in returnResult
                                  group obj by new
                                  {
                                      obj.QuizID,
                                      obj.UserId,
                                      obj.QuizName,
                                      obj.UserName,
                                      obj.Status,
                                      obj.ExpiryDate
                                  } into grp
                                  select grp.First();

            // records to display            
            var filteredRecords = displayedRecord.Skip(param.iDisplayStart).Take(param.iDisplayLength);
            if (param.iDisplayLength == -1)
                filteredRecords = displayedRecord;

            var result = from obj in filteredRecords
                         select new[]
                     {
                        obj.QuizID.ToString(),
                        obj.UserId.ToString(),
                        obj.QuizName,
                        obj.UserName,
                        obj.Status,
                        obj.ExpiryDate.HasValue?string.Format("{0:dd/MM/yyyy}",obj.ExpiryDate):""
                     };

            return Json(new
            {
                sEcho = param.sEcho,
                iTotalRecords = displayedRecord.Count(),
                iTotalDisplayRecords = displayedRecord.Count(),
                aaData = result
            },
                           JsonRequestBehavior.AllowGet);
        }

        public ActionResult AjaxHandlerUserQuizDetail(int QuizID, int UserId,int CategoryId,int GroupId)
        {
            var result = from obj in db.GetUserQuizReport(CategoryId , QuizID, GroupId, Convert.ToInt32(Session["UserID"]), false).Where(x => x.QuizID == QuizID && x.UserId == UserId)
                         select new GetUserQuizReport_Result
                         {
                             QuestionText = obj.QuestionText,
                             AnswerText = obj.AnswerText,
                             UserAnswers = obj.UserAnswers,
                             IsCorrect = obj.IsCorrect
                         };

            return Json(result, JsonRequestBehavior.AllowGet);
        }
        
        public ActionResult GetQuizForGroupByID(string gid)
        {
            long id = Convert.ToInt64(gid);
            string str = "<option value=''>All</option>";

            if (id > 0)
            {
                var isUserAssign = db.Groups.Where(a => a.GroupID == id).FirstOrDefault();
                if (isUserAssign != null && isUserAssign.Organisation != null && !isUserAssign.Organisation.IsUserAssignment)
                {
                    var filterQuiz = db.GroupQuizs.Where(a => a.AssignedStatus == true && a.GroupID == id).OrderBy(b => b.Quize.QuizName).ToList();
                    foreach (var qz in filterQuiz)
                    {
                        str += "<option value='" + qz.QuizID + "'>" + qz.Quize.QuizName + "</option>";
                    }
                }
                else
                {
                    var filterQuiz = db.Quizes.OrderBy(b => b.QuizName).ToList();
                    foreach (var qz in filterQuiz)
                    {
                        str += "<option value='" + qz.QuizID + "'>" + qz.QuizName + "</option>";
                    }
                }
            }
            else
            {
                var filterQuiz = db.Quizes.OrderBy(b => b.QuizName).ToList();
                foreach (var qz in filterQuiz)
                {
                    str += "<option value='" + qz.QuizID + "'>" + qz.QuizName + "</option>";
                }
            }

            return Json(new { data = str }, JsonRequestBehavior.AllowGet);

        }

        #endregion
        #region // Group reports
        public ActionResult GroupReport()
        {
            return View();
        }

        public ActionResult AjaxHandlerGroupReport(jQueryDataTableParamModel param)
        {
            var returnResult = (from obj in db.GetGroupUserCourse()
                                select obj
                                ).ToList();

            // records to display            
            var displayedRecord = returnResult.Skip(param.iDisplayStart).Take(param.iDisplayLength);
            if (param.iDisplayLength == -1)
                displayedRecord = returnResult;

            var result = from obj in displayedRecord
                         select new[] {
                              obj.GroupName,
                              obj.Users.ToString(),
                              obj.Courses.ToString(),
                              obj.GroupID.ToString()
                          };

            return Json(new
            {
                sEcho = param.sEcho,
                iTotalRecords = returnResult.Count(),
                iTotalDisplayRecords = returnResult.Count(),
                aaData = result
            },
                           JsonRequestBehavior.AllowGet);
        }

        #endregion
        #region // info reports

        public ActionResult UserInfo(int id=0)
        {
            UserReportInfo user = new UserReportInfo();
            user.UserId = id;
            //var users = db.UserProfiles.Find(id);
            //user.Email = users.EmailAddress;
            //user.FirstName = users.FirstName;
            //user.LastName = users.LastName;
            var returnResult = (from obj in db.GetUserCourseReport(0, 0, 0, Convert.ToInt32(Session["UserID"]), false, 0, id.ToString())
                                group obj by new
                                {
                                    obj.EmailAddress,
                                    obj.FirstName,
                                    obj.LastName
                                }
                         into gs
                                select new UserReport
                                {
                                    Email = gs.Key.EmailAddress,
                                    FirstName = gs.Key.FirstName,
                                    LastName = gs.Key.LastName,
                                    Courses = gs.Count(c => c.CourseName != null),
                                    Completed = gs.Count(c => c.Status == "Completed"),
                                    Notstarted = gs.Count(c => c.Status == "Not started"),
                                    Inprogress = gs.Count(c => c.Status == "In progress")

                                }).FirstOrDefault();
            if (returnResult == null || id==0)
            {
                return HttpNotFound();
            }

            user.UserDetails = returnResult;

            return View(user);
        }

        public ActionResult AjaxHandlerUserinfocourseReport(jQueryDataTableParamModel param)
        {

            var returnResult = (from obj in db.GetUserCourseReport(0, 0, 0, Convert.ToInt32(Session["UserID"]), false, 0, param.fCol1)
                                select new UserInfoCourses
                                {
                                    CourseName = obj.CourseName,
                                    EnrolledOn = obj.RegistrationDate.HasValue ? string.Format("{0:dd/MM/yyyy}", obj.RegistrationDate) : "",
                                    Status = obj.Status,
                                    CompletionDate = obj.Completiondate.HasValue ? string.Format("{0:dd/MM/yyyy}", obj.Completiondate) : "",
                                    ExpiryDate = obj.ExpiryDate.HasValue ? string.Format("{0:dd/MM/yyyy}", obj.ExpiryDate) : "",
                                    score = obj.Score.ToString(),
                                    isQuiz = obj.IsQuiz


                                }).ToList();


            // records to display            
            var displayedRecord = returnResult.Skip(param.iDisplayStart).Take(param.iDisplayLength);
            if (param.iDisplayLength == -1)
                displayedRecord = returnResult;

            var result = from obj in displayedRecord
                         select new[] {
                              obj.CourseName,
                              (obj.isQuiz==0)?db.Courses.Where(c=>c.CourseName==obj.CourseName).Select(S=>S.Category.CategoryName).FirstOrDefault():
                              db.Quizes.Where(c=>c.QuizName==obj.CourseName).Select(S=>S.Category.CategoryName).FirstOrDefault(),
                              obj.EnrolledOn,
                              obj.Status,
                              obj.CompletionDate,
                              obj.ExpiryDate,
                              obj.score,
                              (obj.isQuiz==0)?db.Courses.Where(c=>c.CourseName==obj.CourseName).Select(S=>S.CourseId).FirstOrDefault().ToString():
                              db.Quizes.Where(c=>c.QuizName==obj.CourseName).Select(S=>S.QuizID).FirstOrDefault().ToString(),
                              obj.isQuiz.ToString()
                          };

            return Json(new
            {
                sEcho = param.sEcho,
                iTotalRecords = returnResult.Count(),
                iTotalDisplayRecords = returnResult.Count(),
                aaData = result
            },
                           JsonRequestBehavior.AllowGet);
        }

        public ActionResult CourseInfo(int id=0,int type=0)
        {
            if (id == 0)
            {
                return HttpNotFound();
            }
            var found = (type == 0) ? (db.Courses.Find(id)!=null) : (db.Quizes.Find(id)!=null);
            if (!found)
            {
                return HttpNotFound();
            }
            CourseReportInfo course = new CourseReportInfo();
            course.CourseId = id;
            course.CourseType = type;
            
            course.Subject = (type == 0) ? db.Courses.Find(id).Category.CategoryName : db.Quizes.Find(id).Category.CategoryName;
            
            var returnResult = (from obj in db.GetUserCourseReport(0, id, 0, Convert.ToInt32(Session["UserID"]), false, 0, "0")
                                where obj.IsQuiz==type
                                group obj by new
                                {
                                    obj.CourseName,
                                    obj.IsQuiz
                                }
                         into gs
                                select new CourseReport
                                {
                                    CourseName = gs.Key.CourseName,
                                    isQuiz = gs.Key.IsQuiz,
                                    AssignedUsers = gs.Count(c => c.EmailAddress != null),
                                    Completed = gs.Count(c => c.Status == "Completed"),
                                    NotStarted = gs.Count(c => c.Status == "Not started"),
                                    InProgress = gs.Count(c => c.Status == "In progress")

                                }).FirstOrDefault();

            if (returnResult == null)
            {
                return HttpNotFound();
            }
            course.CourseDetails = returnResult;
            return View(course);
        }

        public ActionResult AjaxHandlerCourseinfoUserReport(jQueryDataTableParamModel param)
        {
            var courseid = Convert.ToInt32(param.fCol1);
            var type = Convert.ToInt32(param.fCol2);
            var returnResult = (from obj in db.GetUserCourseReport(0, courseid, 0, Convert.ToInt32(Session["UserID"]), false, 0, "0")
                                where obj.IsQuiz == type
                                select new CoursesInfoUser
                                {
                                    Email=obj.EmailAddress,
                                    UserName= obj.FirstName+" "+((obj.LastName!=null)?obj.LastName:""),
                                    EnrolledOn = obj.RegistrationDate.HasValue ? string.Format("{0:dd/MM/yyyy}", obj.RegistrationDate) : "",
                                    Status = obj.Status,
                                    CompletionDate = obj.Completiondate.HasValue ? string.Format("{0:dd/MM/yyyy}", obj.Completiondate) : "",
                                    ExpiryDate = obj.ExpiryDate.HasValue ? string.Format("{0:dd/MM/yyyy}", obj.ExpiryDate) : "",
                                    score = obj.Score.ToString()

                                }).ToList();


            // records to display            
            var displayedRecord = returnResult.Skip(param.iDisplayStart).Take(param.iDisplayLength);
            if (param.iDisplayLength == -1)
                displayedRecord = returnResult;

            var result = from obj in displayedRecord
                         select new[] {
                              obj.UserName,
                              obj.EnrolledOn,
                              obj.Status,
                              obj.CompletionDate,
                              obj.ExpiryDate,
                              obj.score,
                              obj.Email,
                              db.UserProfiles.Where(U=>U.EmailAddress==obj.Email).FirstOrDefault().UserId.ToString()
                          };

            return Json(new
            {
                sEcho = param.sEcho,
                iTotalRecords = returnResult.Count(),
                iTotalDisplayRecords = returnResult.Count(),
                aaData = result
            },
                           JsonRequestBehavior.AllowGet);
        }

        public ActionResult GroupInfo(int id=0)
        {
            GroupReportInfo group = new GroupReportInfo();
            group = (from obj in db.GetGroupUserCourse()
                               where obj.GroupID == id
                               select new GroupReportInfo
                               {
                                   GroupId = obj.GroupID,
                                   GroupName = obj.GroupName,
                                   Courses = obj.Courses.ToString(),
                                   Users = obj.Users.ToString()

                               }).FirstOrDefault();
            if (group == null)
            {
                return HttpNotFound();
            }
            
            return View(group);
        }
        #endregion
    }
}