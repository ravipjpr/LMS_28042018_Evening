using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CLSLms;
using LMS.Models;
using System.IO;
using System.Xml;
using CLSScorm.LeopinkLMS;
using System.Configuration;
using System.Globalization;
using System.Xml.Linq;
using System.Data.Entity.Validation;
using ICSharpCode.SharpZipLib.Zip;

namespace LMS.Controllers
{
    [CustomAuthorize]
    public class CourseController : Controller
    {
        private LeopinkLMSDBEntities db = new LeopinkLMSDBEntities();

        # region// Course listing
        /// <summary>
        /// course listing page
        /// </summary>
        /// <returns></returns>
        public ActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// data loading in course listing page
        /// </summary>
        /// <param name="param"> paramater for jquery table parameters like search, sort and etc.</param>
        /// <returns></returns>
        public ActionResult AjaxHandlerCourse(jQueryDataTableParamModel param)
        {
            var sortColumnIndex = Convert.ToInt32(Request["iSortCol_0"]); // sort column index
            Func<Course, string> orderingFunction = (c => sortColumnIndex == 0 ? c.CourseName.TrimEnd().TrimStart().ToLower() :
                                                        sortColumnIndex == 1 ? ((c.Category != null) ? c.Category.CategoryName.TrimEnd().TrimStart().ToLower() : "-") :
                                                        sortColumnIndex == 2 ? ((c.Certificate != null) ? c.Certificate.CertificateName.TrimEnd().TrimStart().ToLower() : "-") :
                                                        sortColumnIndex == 3 ? (c.Status.ToString()) :
                                                        c.CourseName.ToLower());
            var sortDirection = Request["sSortDir_0"]; // sort direction
            IEnumerable<Course> filterCourse = null;

            /// search action
            var currentLoginUser = Convert.ToInt64(Session["UserID"].ToString());
            var Groups = from g in db.UserGroups
                         where g.UserId == currentLoginUser
                         select g.GroupID;
            List<int> lst = Groups.ToList();
            var userrole = Session["UserRoles"].ToString().Contains("1");

            if (!string.IsNullOrEmpty(param.sSearch))
            {
                if (userrole)
                {
                    filterCourse = from Cour in db.Courses
                                   where Cour.IsDeleted == false &&
                                        Cour.CourseName.ToLower().Contains(param.sSearch.ToLower()) ||
                                        Cour.Tags.ToLower().Contains(param.sSearch.ToLower()) ||
                                        ((Cour.Category != null) ? Cour.Category.CategoryName.TrimEnd().TrimStart().ToLower() : "-").Contains(param.sSearch.ToLower()) ||
                                        ((Cour.Certificate != null) ? Cour.Certificate.CertificateName.TrimEnd().TrimStart().ToLower() : "-").Contains(param.sSearch.ToLower())
                                   select Cour;
                }
                else
                {
                    filterCourse = ((from Cour in db.Courses
                                     join GrpCrs in db.GroupCourses
                                     on Cour.CourseId equals GrpCrs.CourseId
                                     where Cour.IsDeleted == false && GrpCrs.AssignedStatus == true && lst.Contains(GrpCrs.GroupID) &&
                                          Cour.CourseName.ToLower().Contains(param.sSearch.ToLower()) ||
                                          ((Cour.Category != null) ? Cour.Category.CategoryName.TrimEnd().TrimStart().ToLower() : "-").Contains(param.sSearch.ToLower()) ||
                                          ((Cour.Certificate != null) ? Cour.Certificate.CertificateName.TrimEnd().TrimStart().ToLower() : "-").Contains(param.sSearch.ToLower())
                                     select Cour).Union
                                   (
                                    from Crs in db.Courses
                                    where Crs.IsDeleted == false && Crs.CreatedById == currentLoginUser &&
                                         Crs.CourseName.ToLower().Contains(param.sSearch.ToLower()) ||
                                         ((Crs.Category != null) ? Crs.Category.CategoryName.TrimEnd().TrimStart().ToLower() : "-").Contains(param.sSearch.ToLower()) ||
                                         ((Crs.Certificate != null) ? Crs.Certificate.CertificateName.TrimEnd().TrimStart().ToLower() : "-").Contains(param.sSearch.ToLower())
                                    select Crs
                                   )).Distinct();
                }
            }
            else
            {
                if (userrole)
                {
                    filterCourse = from Cour in db.Courses
                                   where Cour.IsDeleted == false
                                   orderby Cour.CourseName.ToLower()
                                   select Cour;
                }
                else
                {
                    if (Common.IsIndividual())
                    {
                        filterCourse = db.Courses.Where(a => a.IsFinalized == true && a.IsDeleted == false && a.Status == true).ToList();
                    }
                    else
                    {
                        filterCourse = ((from Cour in db.Courses
                                         join GrpCrs in db.GroupCourses
                                         on Cour.CourseId equals GrpCrs.CourseId
                                         where Cour.IsDeleted == false && GrpCrs.AssignedStatus == true && lst.Contains(GrpCrs.GroupID)
                                         orderby Cour.CourseName.ToLower()
                                         select Cour).Union
                                       (
                                        from Crs in db.Courses
                                        where Crs.IsDeleted == false && Crs.CreatedById == currentLoginUser
                                        orderby Crs.CourseName.ToLower()
                                        select Crs
                                       )).Distinct();
                    }
                }
            }
            var AdminRole = Session["CourseMangerRole"].ToString();

            var satCourse = from element in db.course_state
                            group element by element.courseid
                            into groups
                            select groups.OrderByDescending(p => p.created_date).FirstOrDefault();

            //if (AdminRole == "CC")
            //{
            //    filterCourse = from f in filterCourse
            //                   join cs in satCourse on f.CourseId equals cs.courseid
            //                   where cs.course_state1 == 0 || cs.course_state1 == 4
            //                   select f;
            //}
            //else 

            if (AdminRole == "CR")
            {
                filterCourse = from f in filterCourse
                               join cs in satCourse on f.CourseId equals cs.courseid
                               where cs.course_state1 == 1
                               select f;
            }
            else if (AdminRole == "CP")
            {
                filterCourse = from f in filterCourse
                               join cs in satCourse on f.CourseId equals cs.courseid
                               where cs.course_state1 == 2
                               select f;
            }

            // ordering action
            if (sortColumnIndex == 4)
            {
                filterCourse = (sortDirection == "asc") ? filterCourse.OrderBy(grp => grp.CreationDate).Distinct() : filterCourse.OrderByDescending(grp => grp.CreationDate).Distinct();
            }
            else
                if (sortDirection == "asc")
            {
                filterCourse = filterCourse.OrderBy(orderingFunction).Distinct();
            }
            else if (sortDirection == "desc")
            {
                filterCourse = filterCourse.OrderByDescending(orderingFunction).Distinct();
            }
            // only not deleted course will list in course list.
            filterCourse = filterCourse.Where(cour => cour.IsFinalized == true && cour.IsDeleted == false).ToList().Distinct();

            // records to display            
            var displayedCourse = filterCourse.Skip(param.iDisplayStart).Take(param.iDisplayLength);
            if (param.iDisplayLength == -1)
                displayedCourse = filterCourse;
            var ActiveStatus = LMSResourse.Common.Common.lblActiveStatus;
            var InactiveStatus = LMSResourse.Common.Common.lblInactiveStatus;
            var result = from obj in displayedCourse.ToList()
                         select new[] {

                              obj.CourseName,
                              ((obj.Category==null)?"-": obj.Category.CategoryName),
                              ((obj.CourseType==1 || obj.CourseType==4)? "Scorm 1.2" : (obj.CourseType==2)? "XAPI" : "Custom"),
                              ((obj.Certificate==null)?"-": obj.Certificate.CertificateName),
                              ((obj.Status)?ActiveStatus :InactiveStatus),
                              ((obj.course_state.OrderByDescending(c=>c.created_date).FirstOrDefault().course_state1 == 0) ? LMSResourse.Admin.Course.optReadyReview : (obj.course_state.OrderByDescending(c=>c.created_date).FirstOrDefault().course_state1 == 1)? LMSResourse.Admin.Course.optInReview : (obj.course_state.OrderByDescending(c=>c.created_date).FirstOrDefault().course_state1 == 2) ? LMSResourse.Admin.Course.optReadyPublish : (obj.course_state.OrderByDescending(c=>c.created_date).FirstOrDefault().course_state1 == 3) ?LMSResourse.Admin.Course.optPublished : LMSResourse.Admin.Course.optRejected),
                              string.Format("{0:dd/MM/yyyy}",obj.CreationDate),
                              Convert.ToString( obj.CourseInfoes.Where(courinfo =>courinfo.LanguageId == db.InstanceInfoes.Find(1).DefaultLanguage && courinfo.IsFinalized == true).FirstOrDefault().CourseInfoId) ,
                              obj.FolderLocation.Replace("\\", "/"),
                              obj.WindowHeight == null ? "0" : Convert.ToString(obj.WindowHeight),
                              obj.WindowWidth == null ? "0" : Convert.ToString(obj.WindowWidth),
                              Convert.ToString(obj.CourseId),
                              Convert.ToString(obj.CourseType),
                              Session["EmployeeID"].ToString(),
                              System.Configuration.ConfigurationManager.AppSettings["xapiEndpoint"].ToString(),
                              obj.LaunchFileName,
                              obj.course_state.OrderByDescending(c=>c.created_date).FirstOrDefault().course_state1.ToString(),
                              db.CourseFAQs.Where(F=>F.CourseID==obj.CourseId && F.AnswerText==null).Count().ToString()
                          };
            // return the json object.
            return Json(new
            {
                sEcho = param.sEcho,
                iTotalRecords = filterCourse.Count(),
                iTotalDisplayRecords = filterCourse.Count(),
                aaData = result
            },
                           JsonRequestBehavior.AllowGet);
        }

        #endregion
      
        #region // Create course

        /// <summary>
        /// http get method of create course
        /// </summary>
        /// <returns></returns>
        public ActionResult CreateCourse()
        {
            ViewBag.CategoriesList = new SelectList(db.Categories.Where(cat => cat.Status == true && cat.IsDeleted == false).OrderBy(cat => cat.CategoryName).Select(cat => cat), "CategoryId", "CategoryName");
            ViewBag.CertificateList = new SelectList(db.Certificates.Where(cer => cer.Status == true && cer.IsDeleted == false).OrderBy(cer => cer.CertificateName).Select(cer => cer), "CertificateId", "CertificateName");
            ViewBag.SurveyList = new SelectList(db.Quizes.Where(q => q.IsSurvey == true).OrderBy(qz => qz.QuizName).Select(qz => qz), "QuizID", "QuizName");
            var list = new List<SelectListItem>();
            list.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optMaximise, Value = "false" });
            list.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optUserDefined, Value = "true" });
            ViewBag.BrowseType = list;

            var listfee = new List<SelectListItem>();
            listfee.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optFeeTypeFree, Value = "1", Selected = true });
            listfee.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optFeeTypePaid, Value = "2", Selected = false });
            listfee.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optFeeTypeTrail, Value = "3", Selected = false });
            ViewBag.FeeTypeList = listfee;

            var listmandatory = new List<SelectListItem>();
            listmandatory.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optMandatory, Value = "false", Selected = false });
            listmandatory.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optOptional, Value = "true", Selected = true });
            ViewBag.MandaotryType = listmandatory;

            var listmobile = new List<SelectListItem>();
            listmobile.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optMobileYes, Value = "false", Selected = false });
            listmobile.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optMobileNo, Value = "true", Selected = true });
            ViewBag.MobileCompatibleType = listmobile;

            var model = new Course();
            model.Status = true;
            model.CourseType = 1;
            return View(model);
        }

        /// <summary>
        /// http post method of create course
        /// </summary>
        /// <param name="ObjCourse"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult CreateCourse(Course ObjCourse, string YouTubeURL)
        {
            string absCourseDir = "";
            string relCourseDir = "";
            string absCourseZipPath = "";

            int MaxContentLength = 1024 * 1024 * 200; //50 MB

            string[] AllowedFileExtensions = new string[] { ".zip" };
            string errorMessage1 = "";
            string errorMessage2 = "";
            string fileName = "";
            var ObjNewCourseInfo = new CourseInfo();

            ViewBag.CategoriesList = new SelectList(db.Categories.Where(cat => cat.Status == true && cat.IsDeleted == false).OrderBy(cat => cat.CategoryName).Select(cat => cat), "CategoryId", "CategoryName", ObjCourse.CategoryId);
            ViewBag.CertificateList = new SelectList(db.Certificates.Where(cer => cer.Status == true && cer.IsDeleted == false).OrderBy(cer => cer.CertificateName).Select(cer => cer), "CertificateId", "CertificateName", ObjCourse.CertificateId);
            ViewBag.SurveyList = new SelectList(db.Quizes.Where(q => q.IsSurvey == true).OrderBy(qz => qz.QuizName).Select(qz => qz), "QuizID", "QuizName");
            var list = new List<SelectListItem>();
            list.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optMaximise, Value = "false", Selected = (ObjCourse.IsUserDefined == false) ? true : false });
            list.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optUserDefined, Value = "true", Selected = (ObjCourse.IsUserDefined == true) ? true : false });
            ViewBag.BrowseType = list;

            var listfee = new List<SelectListItem>();
            listfee.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optFeeTypeFree, Value = "1", Selected = (Convert.ToByte(ObjCourse.FeeType) == 1) ? true : false });
            listfee.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optFeeTypePaid, Value = "2", Selected = (Convert.ToByte(ObjCourse.FeeType) == 2) ? true : false });
            listfee.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optFeeTypeTrail, Value = "3", Selected = (Convert.ToByte(ObjCourse.FeeType) == 3) ? true : false });
            ViewBag.FeeTypeList = listfee;

            var listmandatory = new List<SelectListItem>();
            listmandatory.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optMandatory, Value = "false", Selected = (Convert.ToBoolean(ObjCourse.Mandaotry) == false) ? true : false });
            listmandatory.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optOptional, Value = "true", Selected = (Convert.ToBoolean(ObjCourse.Mandaotry) == true) ? true : false });
            ViewBag.MandaotryType = listmandatory;

            var listmobile = new List<SelectListItem>();
            listmobile.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optMobileYes, Value = "false", Selected = (Convert.ToBoolean(ObjCourse.IsMobile) == false) ? true : false });
            listmobile.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optMobileNo, Value = "true", Selected = (Convert.ToBoolean(ObjCourse.IsMobile) == true) ? true : false });
            ViewBag.MobileCompatibleType = listmobile;

            if (ModelState.IsValid)
            {
                #region // Server sidde validation for CouseName and Course Description
                if (ObjCourse.CourseName.Trim() == "")
                {
                    ModelState.AddModelError("CourseName", LMSResourse.Admin.Course.msgReqCourseName);
                    return View(ObjCourse);
                }
                else if (ObjCourse.CourseDescription.Trim() == "")
                {
                    ModelState.AddModelError("Description", LMSResourse.Admin.Course.msgReqCourseDesc);
                    return View(ObjCourse);
                }
                #endregion

                var checkCourseName = db.Courses.Where(cer => cer.IsDeleted == false && cer.CourseName.TrimEnd().TrimStart().ToLower() == ObjCourse.CourseName.TrimEnd().TrimStart().ToLower() && cer.Status == true && cer.IsFinalized == true).Select(cer => cer).SingleOrDefault();

                if (checkCourseName == null)
                {
                    ObjCourse.LastModifiedById = Convert.ToInt64(Session["UserID"]);
                    ObjCourse.DateLastModified = DateTime.Now;

                    #region // Process Course file Save to server folders
                    if (Request.Files.Count > 0)
                    {
                        foreach (string file in Request.Files)
                        {
                            HttpPostedFileBase hpf = Request.Files[file];

                            if (hpf.FileName != "" && ObjCourse.CourseType != 4)
                            {
                                fileName = hpf.FileName.Split('\\').Last().ToString();

                                if (Common.IsValidFileName(hpf.FileName.Split('\\').Last().ToString(), true) == false)
                                {
                                    ModelState.AddModelError("FolderLocation", errorMessage1);
                                    //PrepareAvailableLangaugeModel(activity, false);
                                    return View(ObjCourse);
                                }

                                Guid g = Guid.NewGuid();

                                absCourseDir = Path.Combine(Server.MapPath("~") + @"\Content\Uploads\Courses\", "_" + g.ToString());
                                Directory.CreateDirectory(absCourseDir);

                                relCourseDir = Path.Combine(@"\Content\Uploads\Courses\", "_" + g.ToString());

                                if (fileName.Substring(fileName.LastIndexOf('.')).ToLower() == ".mp4")
                                {
                                    fileName = "scorm_video.mp4";
                                    absCourseZipPath = Path.Combine(absCourseDir, @fileName);
                                    hpf.SaveAs(absCourseZipPath);

                                    absCourseZipPath = processVideoUploadContent(true, g.ToString(), ObjCourse.CourseName, fileName, "", "", out fileName);
                                }
                                else
                                {
                                    absCourseZipPath = Path.Combine(absCourseDir, @fileName);
                                    hpf.SaveAs(absCourseZipPath);
                                }

                                Session["pathg"] = g;

                                AllowedFileExtensions = new string[] { ".zip" };
                                MaxContentLength = 1024 * 1024 * 200; //200 MB

                                errorMessage1 = LMSResourse.Admin.Course.msgErrScormNotSupport + " " + string.Join(", ", AllowedFileExtensions);
                                errorMessage2 = LMSResourse.Admin.Course.msgErrScormLarge;

                                if (!AllowedFileExtensions.Contains(fileName.Substring(fileName.LastIndexOf('.')).ToLower()))
                                {
                                    ModelState.AddModelError("FolderLocation", errorMessage1);
                                    //PrepareAvailableLangaugeModel(activity, false);
                                    return View(ObjCourse);
                                }
                                else if (hpf.ContentLength > MaxContentLength)
                                {
                                    ModelState.AddModelError("FolderLocation", errorMessage2);
                                    //PrepareAvailableLangaugeModel(activity, false);
                                    return View(ObjCourse);
                                }
                                ObjCourse.FileSizeInKB = (hpf.ContentLength / 1024);
                            } // File name missing
                            else if (YouTubeURL != "")
                            {
                                Guid g = Guid.NewGuid();
                                absCourseDir = Path.Combine(Server.MapPath("~") + @"\Content\Uploads\Courses\", "_" + g.ToString());
                                relCourseDir = Path.Combine(@"\Content\Uploads\Courses\", "_" + g.ToString());
                                Directory.CreateDirectory(absCourseDir);
                                fileName = "testYoutube.zip";
                                absCourseZipPath = processVideoUploadContent(true, g.ToString(), ObjCourse.CourseName, fileName, "", YouTubeURL, out fileName);
                            }
                            else
                            {
                                ModelState.AddModelError("FolderLocation", LMSResourse.Admin.Course.msgReqCourseFile);
                                return View(ObjCourse);
                            }
                        }
                    }
                    else // error as course file not exist.
                    {
                        ModelState.AddModelError("FolderLocation", LMSResourse.Admin.Course.msgReqCourseFile);
                        return View(ObjCourse);
                    }
                    #endregion

                    ObjCourse.ScoLabel = "Module";
                    ObjCourse.CreatedById = Convert.ToInt64(Session["UserID"]);
                    ObjCourse.CreationDate = DateTime.Now;
                    ObjCourse.IsFinalized = false;
                    ObjCourse.IsDeleted = false;
                    //User Defined
                    if (ObjCourse.IsUserDefined == true)
                    {
                        ObjCourse.WindowHeight = ObjCourse.WindowHeight;
                        ObjCourse.WindowWidth = ObjCourse.WindowWidth;
                    }
                    //Maximise
                    else
                    {
                        ObjCourse.WindowHeight = 0;
                        ObjCourse.WindowWidth = 0;
                    }

                    if (absCourseZipPath != "")
                    {
                        ObjCourse.FolderLocation = relCourseDir;
                        ObjCourse.LaunchFileName = fileName;
                        ObjCourse.IsDeleted = false;
                        db.Courses.Add(ObjCourse);
                        db.SaveChanges();

                        #region /// Create course state //
                        var ObjCourseState = new course_state();
                        ObjCourseState.courseid = ObjCourse.CourseId;
                        ObjCourseState.course_state_comment = "Course Created";
                        ObjCourseState.course_state1 = 0;
                        ObjCourseState.created_date = DateTime.Now;
                        ObjCourseState.created_by = Convert.ToInt64(Session["UserID"]);
                        db.course_state.Add(ObjCourseState);
                        db.SaveChanges();
                        #endregion

                        #region /// Create course info for default language // remove default language info record which are not finalized.

                        var ObjDefaultLanguage = db.InstanceInfoes.Find(1).DefaultLanguage;
                        // First remove the records related to languageid and isfinalised  = 0
                        var CourseinfoIsfinalised0 = from CourInfo in db.CourseInfoes
                                                     where CourInfo.CourseId == ObjCourse.CourseId && CourInfo.LanguageId == ObjDefaultLanguage && CourInfo.IsFinalized == false
                                                     select CourInfo;

                        foreach (var CourInfo in CourseinfoIsfinalised0.ToList())
                        {
                            var actinfoRecord = db.CourseInfoes.Find(CourInfo.CourseInfoId);
                            if (actinfoRecord != null)
                            {
                                db.CourseInfoes.Remove(actinfoRecord);
                                db.SaveChanges();
                            }
                        }


                        ObjNewCourseInfo.CourseId = ObjCourse.CourseId;
                        ObjNewCourseInfo.CourseName = ObjCourse.CourseName;
                        ObjNewCourseInfo.CourseDescription = ObjCourse.CourseDescription;
                        ObjNewCourseInfo.FolderLocation = ObjCourse.FolderLocation;
                        ObjNewCourseInfo.LaunchFileName = ObjCourse.LaunchFileName;
                        ObjNewCourseInfo.FileSizeInKB = ObjCourse.FileSizeInKB;
                        ObjNewCourseInfo.ScoLabel = ObjCourse.ScoLabel;
                        ObjNewCourseInfo.IsFinalized = ObjCourse.IsFinalized;

                        ObjNewCourseInfo.IsUserDefined = ObjCourse.IsUserDefined;
                        ObjNewCourseInfo.WindowHeight = ObjCourse.WindowHeight;
                        ObjNewCourseInfo.WindowWidth = ObjCourse.WindowWidth;

                        ObjNewCourseInfo.PassMarks = ObjCourse.PassMarks;
                        ObjNewCourseInfo.LanguageId = ObjDefaultLanguage;
                        ObjNewCourseInfo.CreationDate = ObjCourse.CreationDate;
                        ObjNewCourseInfo.CreatedById = ObjCourse.CreatedById;

                        db.CourseInfoes.Add(ObjNewCourseInfo);
                        db.SaveChanges();

                        #endregion

                    }
                    else // course folder not exist
                    {
                        ModelState.AddModelError("FolderLocation", LMSResourse.Admin.Course.msgReqCourseFile);
                        return View(ObjCourse);
                    }


                    #region // check valid course file.


                    try
                    {

                        Boolean IsNotValidateZipMsg = false;

                        CLSScorm.LeopinkLMS.ImportCourse objImportCourse = new CLSScorm.LeopinkLMS.ImportCourse(absCourseZipPath, ObjNewCourseInfo.CourseInfoId);

                        objImportCourse.CourseUploadPath = absCourseZipPath;
                        objImportCourse.CourseImport();

                        IsNotValidateZipMsg = objImportCourse.IsValidateZipMsg;

                        DirectoryInfo dir1 = new DirectoryInfo(absCourseDir);
                        FileInfo[] file1 = null;
                        string FileUrl = "", Spath;
                        //FileInfo ff = default(FileInfo);
                        bool FindCRS = false;
                        file1 = dir1.GetFiles((ObjCourse.CourseType == 1 || ObjCourse.CourseType == 4 ? "imsmanifest.xml" : "tincan.xml"));

                        if (file1.Length > 0)
                        {
                            foreach (var fla in file1)
                            {
                                FileUrl = fla.FullName.ToUpper();
                                FindCRS = true;
                            }
                        }
                        else
                        {
                            FindCRS = false;
                        }
                        if (FindCRS)
                        {
                            try
                            {
                                if (ObjCourse.CourseType == 1 || ObjCourse.CourseType == 4)
                                {
                                    //string versionValue = "1.2";
                                    FileUrl = FileUrl.Substring(FileUrl.LastIndexOf("CONTENT\\"));
                                    Spath = Server.MapPath("~");
                                    //Spath = Server.MapPath("..\\..\\");
                                    //Scorm course files are imported, from the path finded in load event,and then it is executed.
                                    CLSScorm.LeopinkLMS.ImportSCORM objImport = new CLSScorm.LeopinkLMS.ImportSCORM(Spath + "/" + FileUrl, FileUrl.Substring(0, FileUrl.LastIndexOf("\\")));
                                    objImport.m_UserId = "1";
                                    objImport.m_navigationType = "F";
                                    objImport.m_courseTitle = ObjNewCourseInfo.CourseName;
                                    objImport.StartImport(Convert.ToInt32(ObjNewCourseInfo.CourseInfoId));
                                }
                                else
                                {
                                    XDocument doc = XDocument.Load(FileUrl);
                                    XElement element = GetElement(doc, "launch");
                                    ObjCourse.LaunchFileName = element.Value;
                                }
                                var objs = db.CourseInfoes.FirstOrDefault(s => s.CourseInfoId == ObjNewCourseInfo.CourseInfoId);
                                if ((objs != null))
                                {
                                    objs.IsFinalized = true;
                                    ObjCourse.IsFinalized = true;
                                    ObjCourse.IsDeleted = false;
                                    db.SaveChanges();
                                }
                                //Session["path"] = "";
                                return RedirectToAction("Index", "Course");
                            }
                            catch (Exception er)
                            {
                                //PrepareAvailableLangaugeModel(activity, false);
                                if (er.Message == "AlreadyExist")
                                {
                                    //ModelState.AddModelError("FolderLocation", "Course already exists. " + er.Message.ToString());
                                    ModelState.AddModelError("FolderLocation", LMSResourse.Admin.Course.msgCourseAlreadyExist);
                                }
                                else if (er.Message == "VersionNotFound")
                                {
                                    ModelState.AddModelError("FolderLocation", LMSResourse.Admin.Course.msgUnSupportedScrom + er.Message.ToString());
                                }
                                else if (er.Message == "VersionNotCorrect")
                                {
                                    ModelState.AddModelError("FolderLocation", LMSResourse.Admin.Course.msgIncorrectVersion + er.Message.ToString());
                                }
                                else
                                {
                                    ModelState.AddModelError("FolderLocation", LMSResourse.Admin.Course.msgErrNotScormCompliant + er.Message.ToString());
                                }
                                return View(ObjCourse);
                            }
                        }
                        else
                        {
                            ModelState.AddModelError("FolderLocation", LMSResourse.Admin.Course.msgErrNotScormCompliant);
                            return View(ObjCourse);
                        }
                    }
                    catch (Exception ex)
                    {

                        ModelState.AddModelError("FolderLocation", ex.Message.ToString());
                        return View(ObjCourse);
                    }

                    #endregion
                }
                else
                {
                    // return model with error message
                    ModelState.AddModelError("CourseName", LMSResourse.Admin.Course.msgDupCourseName);
                }
            }
            return View(ObjCourse);
        }

        private XElement GetElement(XDocument doc, string elementName)
        {
            foreach (XNode node in doc.DescendantNodes())
            {
                if (node is XElement)
                {
                    XElement element = (XElement)node;
                    if (element.Name.LocalName.Equals(elementName))
                        return element;
                }
            }
            return null;
        }
        #endregion

        #region // Edit Course

        /// <summary>
        /// http Get method of edit course
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ActionResult EditCourse(int id = 0)
        {
            Course ObjCourse = db.Courses.Find(id);
            CourseEdit ObjCourseEdit = new CourseEdit();
            if (ObjCourse == null)
            {
                return HttpNotFound();
            }
            // Set the categotries and certificate list to fill in dropdown list on page.
            ViewBag.CategoriesList = new SelectList(db.Categories.Where(cat => cat.Status == true).OrderBy(cat => cat.CategoryName).Select(cat => cat), "CategoryId", "CategoryName", ObjCourse.CategoryId);
            ViewBag.CertificateList = new SelectList(db.Certificates.Where(cer => cer.Status == true && cer.IsDeleted == false).OrderBy(cer => cer.CertificateName).Select(cer => cer), "CertificateId", "CertificateName", ObjCourse.CertificateId);
            ViewBag.SurveyList = new SelectList(db.Quizes.Where(q => q.IsSurvey == true).OrderBy(qz => qz.QuizName).Select(qz => qz), "QuizID", "QuizName");
            var list = new List<SelectListItem>();
            // Set the course window type to fill in dropdown list on page.
            list.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optMaximise, Value = "false", Selected = (ObjCourse.IsUserDefined == false) ? true : false });
            list.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optUserDefined, Value = "true", Selected = (ObjCourse.IsUserDefined == true) ? true : false });
            ViewBag.BrowseType = list;
            var listfee = new List<SelectListItem>();
            listfee.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optFeeTypeFree, Value = "1", Selected = (Convert.ToByte(ObjCourse.FeeType) == 1) ? true : false });
            listfee.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optFeeTypePaid, Value = "2", Selected = (Convert.ToByte(ObjCourse.FeeType) == 2) ? true : false });
            listfee.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optFeeTypeTrail, Value = "3", Selected = (Convert.ToByte(ObjCourse.FeeType) == 3) ? true : false });
            ViewBag.FeeTypeList = listfee;

            var listmandatory = new List<SelectListItem>();
            listmandatory.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optMandatory, Value = "false", Selected = (Convert.ToBoolean(ObjCourse.Mandaotry) == false) ? true : false });
            listmandatory.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optOptional, Value = "true", Selected = (Convert.ToBoolean(ObjCourse.Mandaotry) == true) ? true : false });
            ViewBag.MandaotryType = listmandatory;

            var listmobile = new List<SelectListItem>();
            listmobile.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optMobileYes, Value = "false", Selected = (Convert.ToBoolean(ObjCourse.IsMobile) == false) ? true : false });
            listmobile.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optMobileNo, Value = "true", Selected = (Convert.ToBoolean(ObjCourse.IsMobile) == true) ? true : false });
            ViewBag.MobileCompatibleType = listmobile;

            ViewBag.youtubeURL = "";
            if (!String.IsNullOrEmpty(ObjCourse.FolderLocation))
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(Server.MapPath("~") + ObjCourse.FolderLocation + "\\imsmanifest.xml");
                foreach (XmlNode xnode in doc.ChildNodes[1].ChildNodes)
                {
                    if (xnode.Name == "youtube")
                    {
                        ViewBag.youtubeURL = xnode.ChildNodes[0].InnerText;
                    }
                }
            }
            return View(ObjCourse);

        }


        /// <summary>
        /// http post method of edit course
        /// </summary>
        /// <param name="ObjCourse"></param>
        /// <param name="ModificationOption"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult EditCourse(Course ObjCourse, string ModificationOption, string YouTubeURL)
        {
            Boolean IsMainfestFile = false, IsNotValidateZipMsg = false;
            string virtualPath, filePath;
            XmlDocument doc;
            UpdateItemParser m_ItemParser;
            var ObjNewCourseInfo = new CourseInfo();
            // Set the categotries and certificate list to fill in dropdown list on page and set the selected value from model.
            ViewBag.CategoriesList = new SelectList(db.Categories.Where(cat => cat.Status == true && cat.IsDeleted == false).OrderBy(cat => cat.CategoryName).Select(cat => cat), "CategoryId", "CategoryName", ObjCourse.CategoryId);
            ViewBag.CertificateList = new SelectList(db.Certificates.Where(cer => cer.Status == true && cer.IsDeleted == false).OrderBy(cer => cer.CertificateName).Select(cer => cer), "CertificateId", "CertificateName", ObjCourse.CertificateId);
            ViewBag.SurveyList = new SelectList(db.Quizes.Where(q => q.IsSurvey == true).OrderBy(qz => qz.QuizName).Select(qz => qz), "QuizID", "QuizName");
            var list = new List<SelectListItem>();
            list.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optMaximise, Value = "false", Selected = (ObjCourse.IsUserDefined == false) ? true : false });
            list.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optUserDefined, Value = "true", Selected = (ObjCourse.IsUserDefined == true) ? true : false });
            ViewBag.BrowseType = list;

            var listfee = new List<SelectListItem>();
            listfee.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optFeeTypeFree, Value = "1", Selected = (Convert.ToByte(ObjCourse.FeeType) == 1) ? true : false });
            listfee.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optFeeTypePaid, Value = "2", Selected = (Convert.ToByte(ObjCourse.FeeType) == 2) ? true : false });
            listfee.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optFeeTypeTrail, Value = "3", Selected = (Convert.ToByte(ObjCourse.FeeType) == 3) ? true : false });
            ViewBag.FeeTypeList = listfee;

            var listmandatory = new List<SelectListItem>();
            listmandatory.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optMandatory, Value = "false", Selected = (Convert.ToBoolean(ObjCourse.Mandaotry) == false) ? true : false });
            listmandatory.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optOptional, Value = "true", Selected = (Convert.ToBoolean(ObjCourse.Mandaotry) == true) ? true : false });
            ViewBag.MandaotryType = listmandatory;

            var listmobile = new List<SelectListItem>();
            listmobile.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optMobileYes, Value = "false", Selected = (Convert.ToBoolean(ObjCourse.IsMobile) == false) ? true : false });
            listmobile.Add(new SelectListItem() { Text = LMSResourse.Admin.Course.optMobileNo, Value = "true", Selected = (Convert.ToBoolean(ObjCourse.IsMobile) == true) ? true : false });
            ViewBag.MobileCompatibleType = listmobile;

            if (ModelState.IsValid)
            {
                #region // Server sidde validation for CouseName and Course Description
                if (ObjCourse.CourseName.Trim() == "")
                {
                    ModelState.AddModelError("CourseName", LMSResourse.Admin.Course.msgReqCourseName);
                    return View(ObjCourse);
                }
                else if (ObjCourse.CourseDescription.Trim() == "")
                {
                    ModelState.AddModelError("Description", LMSResourse.Admin.Course.msgReqCourseDesc);
                    return View(ObjCourse);
                }
                #endregion

                var checkCourseName = db.Courses.Where(cer => cer.IsDeleted == false && cer.CourseName.TrimEnd().TrimStart().ToLower() == ObjCourse.CourseName.TrimEnd().TrimStart().ToLower() && cer.Status == true && cer.IsFinalized == true && cer.CourseId != ObjCourse.CourseId).Select(cer => cer).SingleOrDefault();

                if (checkCourseName == null)
                {
                    var obj = db.Courses.Find(ObjCourse.CourseId);
                    var defaultLanguage = db.InstanceInfoes.Find(1).DefaultLanguage;
                    ObjNewCourseInfo = db.CourseInfoes.Where(corinfo => corinfo.CourseId == ObjCourse.CourseId && corinfo.LanguageId == defaultLanguage && (bool)corinfo.IsFinalized == true).FirstOrDefault();
                    if (obj != null)
                    {
                        // update the value of searched course  in course table
                        obj.CourseName = ObjCourse.CourseName;
                        obj.CourseDescription = ObjCourse.CourseDescription;
                        obj.Tags = ObjCourse.Tags;
                        obj.Status = ObjCourse.Status;
                        obj.ScoLabel = ObjCourse.ScoLabel;
                        obj.PassMarks = ObjCourse.PassMarks;
                        obj.CertificateId = ObjCourse.CertificateId;
                        obj.LastModifiedById = Convert.ToInt64(Session["UserID"]);
                        obj.DateLastModified = DateTime.Now;
                        obj.Mandaotry = ObjCourse.Mandaotry;
                        obj.IsMobile = ObjCourse.IsMobile;
                        obj.FeeType = ObjCourse.FeeType;
                        obj.CourseDurationMin = ObjCourse.CourseDurationMin;
                        obj.CourseFees = ObjCourse.CourseFees;
                        obj.CategoryId = ObjCourse.CategoryId;
                        obj.SurveyID = ObjCourse.SurveyID;

                        obj.IsUserDefined = ObjCourse.IsUserDefined;
                        if (obj.IsUserDefined == true)    //User Defined
                        {
                            obj.WindowHeight = ObjCourse.WindowHeight;
                            obj.WindowWidth = ObjCourse.WindowWidth;
                        }
                        else
                        {
                            obj.WindowHeight = 0;
                            obj.WindowWidth = 0;
                        }

                        #region // check course info if not exist then create the record
                        if (ObjNewCourseInfo == null)
                        {
                            ObjNewCourseInfo.CourseId = obj.CourseId;
                            ObjNewCourseInfo.CourseName = obj.CourseName;
                            ObjNewCourseInfo.CourseDescription = obj.CourseDescription;
                            ObjNewCourseInfo.LastModifiedById = obj.LastModifiedById;
                            ObjNewCourseInfo.DateLastModified = obj.DateLastModified;
                            ObjNewCourseInfo.IsUserDefined = obj.IsUserDefined;
                            ObjNewCourseInfo.WindowHeight = obj.WindowHeight;
                            ObjNewCourseInfo.WindowWidth = obj.WindowWidth;
                            ObjNewCourseInfo.IsFinalized = obj.IsFinalized;
                            ObjNewCourseInfo.FileSizeInKB = obj.FileSizeInKB;
                            ObjNewCourseInfo.FolderLocation = obj.FolderLocation;
                            ObjNewCourseInfo.LanguageId = defaultLanguage;
                            ObjNewCourseInfo.PassMarks = obj.PassMarks;
                            ObjNewCourseInfo.ScoLabel = obj.ScoLabel;
                            db.CourseInfoes.Add(ObjNewCourseInfo);
                            db.SaveChanges();
                        }
                        else
                        {
                            ObjNewCourseInfo.CourseId = obj.CourseId;
                            ObjNewCourseInfo.CourseName = obj.CourseName;
                            ObjNewCourseInfo.CourseDescription = obj.CourseDescription;
                            ObjNewCourseInfo.LastModifiedById = obj.LastModifiedById;
                            ObjNewCourseInfo.DateLastModified = obj.DateLastModified;
                            ObjNewCourseInfo.IsUserDefined = obj.IsUserDefined;
                            ObjNewCourseInfo.WindowHeight = obj.WindowHeight;
                            ObjNewCourseInfo.WindowWidth = obj.WindowWidth;

                        }
                        #endregion


                        if (ModificationOption == "0")
                        {
                            db.SaveChanges();
                            return RedirectToAction("Index", "Course");
                        }
                        else if (ModificationOption == "1" || ModificationOption == "2")
                        {
                            // find the file in post method
                            if (Request.Files.Count > 0)
                            {
                                // check each file in post method
                                foreach (string file in Request.Files)
                                {
                                    HttpPostedFileBase hpf = Request.Files[file];
                                    if (hpf.FileName != "" || YouTubeURL != "")
                                    {

                                        int MaxContentLength = 1024 * 1024 * 200; //200 MB file size
                                        string[] AllowedFileExtensions = new string[] { ".zip", ".mp4" }; // file extention for course upload
                                        string errorMessage1 = LMSResourse.Admin.Course.msgErrScormNotSupport + " " + string.Join(", ", AllowedFileExtensions);
                                        string errorMessage2 = LMSResourse.Admin.Course.msgErrScormLarge;

                                        string fileName = (ObjCourse.CourseType == 4 ? obj.LaunchFileName : hpf.FileName.Split('\\').Last().ToString()); // file name of uploaded file.
                                        if (Common.IsValidFileName(fileName, true))
                                        {
                                            // check the allowable extention
                                            if (!AllowedFileExtensions.Contains(fileName.Substring(fileName.LastIndexOf('.')).ToLower()))
                                            {
                                                ModelState.AddModelError("FolderLocation", errorMessage1);
                                                return View(ObjCourse);
                                            }
                                            // check the allowalable course file size.
                                            else if (hpf.ContentLength > MaxContentLength)
                                            {
                                                ModelState.AddModelError("FolderLocation", errorMessage2);
                                                return View(ObjCourse);
                                            }
                                            try
                                            {
                                                var objs = db.s_courseInfo.FirstOrDefault(s => s.masterCourseId == ObjNewCourseInfo.CourseInfoId);

                                                if (objs != null || ObjCourse.CourseType == 2)
                                                {
                                                    if (ModificationOption == "2")
                                                    {
                                                        Guid g = Guid.NewGuid(); //
                                                        //virtualPath = "~/Content/Uploads/Contents/Courses/" + "_" + g;
                                                        virtualPath = "~/Content/Uploads/Courses/" + "_" + g;
                                                        filePath = Server.MapPath(virtualPath);
                                                        Directory.CreateDirectory(filePath); // creates the directory if directory not exist.

                                                        if (fileName.Substring(fileName.LastIndexOf('.')).ToLower() == ".mp4")
                                                        {
                                                            fileName = "scorm_video.mp4";
                                                            filePath = filePath + "\\" + fileName;
                                                            hpf.SaveAs(filePath); // save file to directory
                                                            filePath = processVideoUploadContent(true, g.ToString(), ObjCourse.CourseName, fileName, "", "", out fileName);
                                                        }
                                                        else
                                                        {
                                                            filePath = filePath + "\\" + fileName;
                                                            hpf.SaveAs(filePath); // save file to directory
                                                        }

                                                        CLSScorm.LeopinkLMS.ImportCourse objImportCourse = new CLSScorm.LeopinkLMS.ImportCourse(filePath, ObjNewCourseInfo.CourseInfoId);
                                                        objImportCourse.CourseUploadPath = filePath;
                                                        objImportCourse.CourseImport();
                                                        IsNotValidateZipMsg = objImportCourse.IsValidateZipMsg;
                                                        IsMainfestFile = true;
                                                        if (IsNotValidateZipMsg == true)
                                                        {
                                                            string DirPath = Server.MapPath(virtualPath);

                                                            DirectoryInfo dir1 = new DirectoryInfo(DirPath);
                                                            FileInfo[] file1 = null;
                                                            string FileUrl = "", Spath;
                                                            //FileInfo ff = default(FileInfo);
                                                            bool FindCRS = false;
                                                            file1 = dir1.GetFiles((ObjCourse.CourseType == 1 || ObjCourse.CourseType == 4 ? "imsmanifest.xml" : "tincan.xml"));
                                                            if (file1.Length > 0) // check the zip file contains imsmanifest.xml file
                                                            {
                                                                foreach (var ff_loopVariable in file1)
                                                                {
                                                                    FileUrl = ff_loopVariable.FullName.ToUpper();
                                                                    FindCRS = true;
                                                                }
                                                            }
                                                            else
                                                            {
                                                                FindCRS = false;
                                                            }

                                                            if (FindCRS) // if file contains manifes file then proceed...
                                                            {
                                                                try
                                                                {
                                                                    if (ObjCourse.CourseType == 1 || ObjCourse.CourseType == 4)
                                                                    {
                                                                        FileUrl = FileUrl.Substring(FileUrl.LastIndexOf("CONTENT\\"));
                                                                        Spath = Server.MapPath("~");
                                                                        CLSScorm.LeopinkLMS.UpdateScorm objImport = new CLSScorm.LeopinkLMS.UpdateScorm(Spath + "\\" + FileUrl, FileUrl.Substring(0, FileUrl.LastIndexOf("\\")));
                                                                        objImport.m_UserId = "1";
                                                                        objImport.m_courseTitle = obj.CourseName;
                                                                        try
                                                                        {
                                                                            objImport.UpdateScorm(Convert.ToInt32(ObjNewCourseInfo.CourseInfoId)); // import the couse file by import course module .
                                                                            string oldPath = objs.courseRelationalPath;
                                                                            filePath = Server.MapPath("~") + "\\" + oldPath.Replace("/", "\\"); ;
                                                                            Directory.Delete(filePath, true);

                                                                            obj.FolderLocation = Path.Combine(@"\Content\Uploads\Courses\", "_" + g.ToString()); ;
                                                                            db.SaveChanges();
                                                                        }
                                                                        catch (Exception ex)
                                                                        {
                                                                            if (ex.Message == "VersionNotFound")
                                                                                errorMessage1 = LMSResourse.Admin.Course.msgUnSupportedScrom;
                                                                            else if (ex.Message == "VersionNotCorrect")
                                                                                errorMessage1 = LMSResourse.Admin.Course.msgIncorrectVersion;
                                                                            else
                                                                                errorMessage1 = LMSResourse.Admin.Course.msgErrNotScormCompliant;
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        XDocument xdoc = XDocument.Load(FileUrl);
                                                                        XElement element = GetElement(xdoc, "launch");
                                                                        obj.FolderLocation = Path.Combine(@"\Content\Uploads\Courses\", "_" + g.ToString());
                                                                        obj.LaunchFileName = element.Value;
                                                                        db.SaveChanges();
                                                                    }
                                                                }
                                                                catch (Exception)
                                                                {
                                                                    ModelState.AddModelError("FolderLocation", LMSResourse.Admin.Course.msgErrNotScormCompliant);
                                                                    return View(ObjCourse);
                                                                }
                                                            }
                                                            else
                                                            {
                                                                ModelState.AddModelError("FolderLocation", LMSResourse.Admin.Course.msgErrNotScormCompliant);
                                                                return View(ObjCourse);
                                                            }
                                                        }
                                                    }
                                                    else if (ModificationOption == "1") // if course content is updated
                                                    {
                                                        virtualPath = objs.courseRelationalPath;
                                                        filePath = Server.MapPath("~") + "\\" + virtualPath.Replace("/", "\\");

                                                        if (fileName.Substring(fileName.LastIndexOf('.')).ToLower() == ".mp4")
                                                        {
                                                            fileName = "scorm_video.mp4";
                                                            if (!Directory.Exists(Server.MapPath("~/" + virtualPath + "/temp")))
                                                            {
                                                                Directory.CreateDirectory(Server.MapPath("~/" + virtualPath + "/temp"));
                                                            }
                                                            hpf.SaveAs(filePath + "\\temp\\" + fileName);

                                                            filePath = processVideoUploadContent(false, "", ObjCourse.CourseName, fileName, virtualPath, "", out fileName);
                                                        }
                                                        else if (YouTubeURL != "")
                                                        {
                                                            filePath = processVideoUploadContent(false, "", ObjCourse.CourseName, fileName, virtualPath, YouTubeURL, out fileName);
                                                        }
                                                        else
                                                        {
                                                            hpf.SaveAs(filePath + "\\" + fileName);
                                                        }

                                                        if (ObjCourse.CourseType != 4)
                                                        {
                                                            CLSScorm.LeopinkLMS.ImportCourse objImportCourse = new CLSScorm.LeopinkLMS.ImportCourse(filePath + "\\" + hpf.FileName, ObjNewCourseInfo.CourseInfoId);
                                                            objImportCourse.CourseUploadPath = filePath + "\\" + fileName;
                                                            objImportCourse.ScormCourseImport(); // import the course from scorm module.
                                                            IsNotValidateZipMsg = objImportCourse.IsValidateZipMsg;
                                                            if ((objImportCourse.CheckValidFile == false)) // check the file is validated.
                                                            {
                                                                IsMainfestFile = false;
                                                            }
                                                            else
                                                            {
                                                                IsMainfestFile = true;
                                                            }
                                                            XmlTextReader tr = default(XmlTextReader);
                                                            tr = new XmlTextReader(filePath + "\\imsmanifest.xml");
                                                            doc = new XmlDocument();
                                                            doc.Load(tr);
                                                            int iCount = 0;
                                                            for (iCount = 0; iCount <= 100; iCount++)
                                                            {
                                                                if (doc.ChildNodes[iCount].Name.ToLower() == "manifest")
                                                                {
                                                                    break; // TODO: might not be correct. Was : Exit For
                                                                }
                                                            }
                                                            //Spath & "\" & FPath, FPath.Substring(0, FPath.LastIndexOf("\")
                                                            m_ItemParser = new CLSScorm.LeopinkLMS.UpdateItemParser(doc.ChildNodes[iCount], virtualPath.Substring(0, virtualPath.LastIndexOf("/")), virtualPath);
                                                            m_ItemParser.m_UserId = "1";
                                                            m_ItemParser.m_MasterCourseId = Convert.ToInt32(objs.masterCourseId);
                                                            //m_ItemParser.m_version = 1.2
                                                            m_ItemParser.m_courseTitle = objs.courseTitle;
                                                            m_ItemParser.UpdateCourse(); /// record updated om scorm tables
                                                            tr.Close();
                                                        }
                                                        db.SaveChanges();
                                                    }
                                                }
                                                else
                                                {
                                                    ModelState.AddModelError("FolderLocation", LMSResourse.Admin.Course.msgReqCourseFile);
                                                    return View(ObjCourse);
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                if (IsNotValidateZipMsg == false)
                                                {
                                                    if ((IsMainfestFile == false))
                                                        errorMessage1 = LMSResourse.Admin.Course.msgNoManifiest;
                                                    //errorMessage1 = "";
                                                    else
                                                        errorMessage1 = LMSResourse.Admin.Course.msgPacInvalidFormat;
                                                    //errorMessage1 = "";
                                                }
                                                else
                                                    errorMessage1 = LMSResourse.Admin.Course.msgIncorrectVersion;


                                                ModelState.AddModelError("FolderLocation", errorMessage1);
                                                return View(ObjCourse);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        ModelState.AddModelError("FolderLocation", LMSResourse.Admin.Course.msgReqCourseFile);
                                        return View(ObjCourse);
                                    }
                                }
                            }
                            else
                            {

                                ModelState.AddModelError("FolderLocation", LMSResourse.Admin.Course.msgReqCourseFile);
                                return View(ObjCourse);
                            }
                            return RedirectToAction("Index", "Course");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("CourseName", LMSResourse.Admin.Course.msgInvalidCourse);
                    }
                }
                else
                {
                    ModelState.AddModelError("CourseName", LMSResourse.Admin.Course.msgDupCourseName);
                }
            }
            return View(ObjCourse);
        }
        #endregion
		
		#region // Delete course
        /// <summary>
        /// Delete a particualr course.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public string DeleteCourse(int id = 0)
        {
            var currentLoginUser = Convert.ToInt64(Session["UserID"].ToString());
            var CourseExist = db.Courses.Find(id);
            if (CourseExist != null)
            {
                var CourseLink = from x in db.Courses
                                 join y in db.UserCourses on x.CourseId equals y.CourseId
                                 join z in db.UserProfiles on y.UserId equals z.UserId
                                 where x.CourseId == CourseExist.CourseId && x.IsFinalized == true && x.IsDeleted == false && y.AssignedStatus == true && z.IsDelete == false
                                 select x;

                var CourseGroupLink = from c in db.Courses
                                      join gc in db.GroupCourses on c.CourseId equals gc.CourseId
                                      join ug in db.UserGroups on gc.GroupID equals ug.GroupID
                                      join up in db.UserProfiles on ug.UserId equals up.UserId
                                      where c.CourseId == CourseExist.CourseId && c.IsFinalized == true && c.IsDeleted == false && up.IsDelete == false && gc.AssignedStatus == true
                                      select c;

                if (CourseLink.Count() == 0 && CourseGroupLink.Count() == 0)
                {
                    CourseExist.IsDeleted = true;
                    CourseExist.DeleteInformation = " : " + CourseExist.CourseName + " is delete by userName : " + db.UserProfiles.Find(currentLoginUser).EmailAddress + " on date" + DateTime.Now.ToString();
                    db.SaveChanges();
                }
                else
                {
                    return string.Format(LMSResourse.Admin.Course.msgDeleteCourse, CourseExist.CourseName);
                }
            }
            else
                return LMSResourse.Admin.Course.msgInvalidCourse;
            return "";

        }
        #endregion

        #region // Course State
        /// <summary>
        /// Delete a particualr course.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public string CourseState(byte stateid, int id = 0)
        {
            var currentLoginUser = Convert.ToInt64(Session["UserID"].ToString());
            var CourseExist = db.Courses.Find(id);
            if (CourseExist != null)
            {
                var ObjCourseState = new course_state();
                ObjCourseState.courseid = CourseExist.CourseId;
                if (stateid == 1)
                {
                    ObjCourseState.course_state_comment = "In review";
                }
                else if (stateid == 2)
                {
                    ObjCourseState.course_state_comment = "Ready to publish";
                }
                else if (stateid == 3)
                {
                    ObjCourseState.course_state_comment = "Published";
                }
                else if (stateid == 4)
                {
                    ObjCourseState.course_state_comment = "Rejected";
                }
                ObjCourseState.course_state1 = stateid;
                ObjCourseState.created_date = DateTime.Now;
                ObjCourseState.created_by = currentLoginUser;
                db.course_state.Add(ObjCourseState);
                db.SaveChanges();
            }
            else
                return LMSResourse.Admin.Course.msgInvalidCourse;
            return "";

        }
        #endregion
        public string Getmaxorassigned(string GroupName, int n)
        {

            var str = GroupName.Split('(')[1].Split(')')[0].Split('/');

            return n == 1 ? str[0].ToString() : str[1].ToString();
        }

        public void uploadnow(HttpPostedFileWrapper upload)
        {
            if (upload != null)
            {
                var AllowedFileExtensions = new string[] { ".jpg", ".png", ".gif" };
                if (!AllowedFileExtensions.Contains(Path.GetExtension(upload.FileName).ToLower()))
                {
                    HttpContext.Response.Write("<script>alert('Invalid file.')</script>");
                }
                else
                {
                    var objInstance = (from o in db.InstanceInfoes
                                       where o.InstanceID == 1
                                       select new { o.URL }).FirstOrDefault();
                    if (objInstance != null)
                    {
                        string imgurl = objInstance.URL;
                        string ImageName = upload.FileName;
                        imgurl = imgurl + "/Content/Uploads/Images/" + ImageName;
                        string path = System.IO.Path.Combine(Server.MapPath("~/Content/Uploads/Images"), ImageName);
                        upload.SaveAs(path);
                        String strPathAndQuery = Request.Url.PathAndQuery;
                        String strUrl = Request.Url.AbsoluteUri.Replace(strPathAndQuery, "/") + Request.ApplicationPath;
                        HttpContext.Response.Write("<script>window.parent.updateValue('1','" + imgurl + "')</script>");
                    }
                }
            }
        }

        public ActionResult uploadPartial()
        {
            var appData = Server.MapPath("~/Content/Uploads/Images");
            var images = Directory.GetFiles(appData).Where(name => !name.EndsWith(".config")).Select(x => new imagesviewmodel
            {
                Url = Url.Content("~/Content/Uploads/Images/" + Path.GetFileName(x))
            });
            return View(images);
        }

        /// <summary>
        /// Copies the contents of a folder, including subfolders to an other folder, overwriting existing files
        /// </summary>
        /// <param name="sourceFolder"></param>
        /// <param name="destinationFolder"></param>
        private void CopyFolderContents(string sourceFolder, string destinationFolder)
        {
            if (Directory.Exists(sourceFolder))
            {
                // Copy folder structure
                foreach (string sourceSubFolder in Directory.GetDirectories(sourceFolder, "*", SearchOption.AllDirectories))
                {
                    Directory.CreateDirectory(sourceSubFolder.Replace(sourceFolder, destinationFolder));
                }
                // Copy files
                foreach (string sourceFile in Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories))
                {
                    string destinationFile = sourceFile.Replace(sourceFolder, destinationFolder);
                    System.IO.File.Copy(sourceFile, destinationFile, true);
                }
            }
        }

        private string processVideoUploadContent(bool fullModification, string g, string courseTitle, string fileName, string virtualPath, string youTubeURL, out string modifiedFileName)
        {
            string absCourseZipPath = "";
            string copyDefaultFolder = (youTubeURL == "" ? "TempScormSource" : "YouTube_Video_Scorm");

            if (fullModification)
            {
                fileName = fileName.Substring(0, fileName.LastIndexOf('.'));

                CopyFolderContents(Server.MapPath("~") + Path.Combine(@"\Content\Uploads\Courses\", copyDefaultFolder),
                    Server.MapPath("~") + Path.Combine(@"\Content\Uploads\Courses\", "_" + g.ToString()));

                modifyCourseManifest(Server.MapPath("~") + Path.Combine(@"\Content\Uploads\Courses\", "_" + g.ToString()), courseTitle, youTubeURL);

                FastZip z = new FastZip();
                z.CreateZip(Server.MapPath("~") + @"\Content\Uploads\Courses\" + "_" + g.ToString() + "_" + fileName,
                    Server.MapPath("~") + @"\Content\Uploads\Courses\" + "_" + g.ToString(), false, "");

                System.IO.File.Move(Server.MapPath("~") + @"\Content\Uploads\Courses\" + "_" + g.ToString() + "_" + fileName,
                    Server.MapPath("~") + @"\Content\Uploads\Courses\" + "_" + g.ToString() + @"\" + fileName + ".zip");

                absCourseZipPath = Server.MapPath("~") + @"\Content\Uploads\Courses\" + "_" + g.ToString() + @"\" + fileName + ".zip";
                fileName = fileName + ".zip";
            }
            else if (youTubeURL == "")
            {
                //Edit content
                modifyCourseManifest(Server.MapPath("~/" + virtualPath) + "/", courseTitle, "");
                fileName = fileName.Substring(0, fileName.LastIndexOf('.'));

                FastZip z = new FastZip();
                z.CreateZip(Server.MapPath("~/" + virtualPath) + "/" + fileName,
                    Server.MapPath("~/" + virtualPath + "/temp"), false, "");

                System.IO.File.Move(Server.MapPath("~/" + virtualPath) + "/" + fileName,
                    Server.MapPath("~/" + virtualPath + "/") + fileName + ".zip");

                System.IO.DirectoryInfo di = new DirectoryInfo(Server.MapPath("~/" + virtualPath + "/temp"));
                if (di.Exists)
                {
                    di.Delete(true);
                }

                absCourseZipPath = Server.MapPath("~/" + virtualPath + "/");
                fileName = fileName + ".zip";
            }
            else
            {
                modifyCourseManifest(Server.MapPath("~/" + virtualPath) + "/", courseTitle, youTubeURL);
                absCourseZipPath = Server.MapPath("~/" + virtualPath + "/");
                fileName = fileName + ".zip";
            }

            modifiedFileName = fileName;
            return absCourseZipPath;
        }

        private void modifyCourseManifest(string courseDirPath, string courseTitle, string youTubeURL)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(courseDirPath + "\\imsmanifest.xml");
            bool isyoutubeExists = false;

            foreach (XmlNode xnode in doc.ChildNodes[1].ChildNodes)
            {
                if (xnode.Name == "organizations")
                {
                    xnode.ChildNodes[0].ChildNodes[0].InnerText = courseTitle;
                    xnode.ChildNodes[0].ChildNodes[1].ChildNodes[0].ChildNodes[0].InnerText = courseTitle;
                }
                else if (xnode.Name == "youtube")
                {
                    xnode.ChildNodes[0].InnerText = youTubeURL;
                    isyoutubeExists = true;
                }
            }

            if (youTubeURL.Trim() != "" && isyoutubeExists == false)
            {
                XmlElement elem = doc.CreateElement("youtube");
                elem.InnerText = youTubeURL;
                doc.DocumentElement.AppendChild(elem);
            }
            doc.Save(courseDirPath + "\\imsmanifest.xml");
        }
		
		#region // Add course to group 
        /// <summary>
        /// course assigned to course
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ActionResult AssignedCourseToGroup(int id = 0)
        {
            var Courseexist = db.Courses.Find(id); // find the course 
            Course_Assigned_T_Group us = new Course_Assigned_T_Group();

            if (Courseexist != null)
            {
                if (Courseexist.IsFinalized == true && Courseexist.IsDeleted == false) // check the course is not deleted.
                {
                    us.CourseId = Courseexist.CourseId;
                    us.CourseName = Courseexist.CourseName;
                    us.DateFormatForClientSide = ConfigurationManager.AppSettings["dateformatForCalanderClientSide"].ToString(); // sets the client side date format.
                }
                else // if course not exist then redirect it to course index page.
                {
                    return RedirectToAction("Index", "Course");
                }

            }
            return View(us);
        }

        /// <summary>
        /// Get the data of course assigned to groups
        /// </summary>
        /// <param name="param"></param>
        /// <returns></returns>
        public ActionResult AjaxHandlerCourseAssignedGroups(jQueryDataTableParamModel param)
        {

            var sortColumnIndex = Convert.ToInt32(Request["iSortCol_0"]);
            Func<Assigned_Course_T_Group, string> orderingFunction = (c => sortColumnIndex == 0 ? c.GroupName.TrimEnd().TrimStart().ToLower() :
                                                            sortColumnIndex == 1 ? c.OrganisationName.TrimEnd().TrimStart().ToLower() :
                                                            sortColumnIndex == 2 ? c.GroupManager.TrimEnd().TrimStart().ToLower() :
                                                            sortColumnIndex == 3 ? c.EmailAddress.TrimEnd().TrimStart().ToLower() :
                                                            sortColumnIndex == 5 ? c.AssignedStatus.ToString() :
                                                            c.GroupName.ToLower());
            var sortDirection = Request["sSortDir_0"];// get the sort direction.
            IEnumerable<Assigned_Course_T_Group> filterUserAssignedCourse = null;
            long? CourseId = Int64.Parse(param.iD.ToString());
            int languageId = 0;
            languageId = int.Parse(Session["LanguageId"].ToString());
            try
            {
                var GetResult = db.GetAssignedCourseGroup(CourseId, Convert.ToInt64(Session["UserID"]), languageId); // get the date from stored procedure.
                var tempx = from x in GetResult
                            select new Assigned_Course_T_Group
                            {
                                GroupId = x.GroupID,
                                GroupName = x.GroupName,
                                OrganisationName = x.OrganisationName,
                                GroupManager = x.GroupManager,
                                EmailAddress = x.EmailAddress,
                                MaxUsers = Convert.ToString(x.MaxUsers),
                                AssignedUsers = Convert.ToString(x.AssignedUsers),
                                ExpiryDate = x.ExpiryDate,
                                AssignedStatus = x.AssignedStatus == true ? true : false
                            };
                filterUserAssignedCourse = tempx.ToList<Assigned_Course_T_Group>();

                /// search action
                if (!string.IsNullOrEmpty(param.sSearch))
                {
                    filterUserAssignedCourse = from x in filterUserAssignedCourse
                                               where x.GroupName.ToLower().Contains(param.sSearch.ToLower()) || x.OrganisationName.ToLower().Contains(param.sSearch.ToLower()) || x.GroupManager.ToLower().Contains(param.sSearch.ToLower()) || x.EmailAddress.ToLower().Contains(param.sSearch.ToLower())
                                               select x;

                }
                else
                {
                    filterUserAssignedCourse = from x in filterUserAssignedCourse
                                               select x;
                }

                // ordering action
                if (sortDirection == "asc")
                {
                    filterUserAssignedCourse = filterUserAssignedCourse.OrderBy(orderingFunction);
                }
                else if (sortDirection == "desc")
                {
                    filterUserAssignedCourse = filterUserAssignedCourse.OrderByDescending(orderingFunction);
                }

                filterUserAssignedCourse = filterUserAssignedCourse.ToList();

                // records to display            
                var displayedUserAssignedCourse = filterUserAssignedCourse.Skip(param.iDisplayStart).Take(param.iDisplayLength);
                if (param.iDisplayLength == -1)
                    displayedUserAssignedCourse = filterUserAssignedCourse;

                // create return object.
                var result = from obj in displayedUserAssignedCourse.ToList()
                             select new[] {
                              obj.GroupName,
                              obj.OrganisationName,
                              obj.GroupManager,
                              obj.EmailAddress,
                              obj.ExpiryDate==null?"":((DateTime)obj.ExpiryDate).ToString(ConfigurationManager.AppSettings["dateformatForCalanderServerSide"].ToString(), CultureInfo.InvariantCulture),
                              obj.AssignedStatus.ToString(),
                              Convert.ToString(obj.GroupId),
                              obj.MaxUsers,
                              obj.AssignedUsers
                          };
                // return the object in json format.
                return Json(new
                {
                    sEcho = param.sEcho,
                    iTotalRecords = filterUserAssignedCourse.Count(),
                    iTotalDisplayRecords = filterUserAssignedCourse.Count(),
                    aaData = result
                },
                               JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {

                },
                                  JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// http post method of assign course to groups
        /// </summary>
        /// <param name="model"></param>
        [HttpPost]
        public void AssignCourseToGroup(List<Submit_Course_Assignment_T_Group> model)
        {
            foreach (var x in model)
            {
                var RecodrExist = db.GroupCourses.Where(y => y.GroupID == x.GroupId && y.CourseId == x.CourseId).FirstOrDefault();
                // check record exist or not
                if (RecodrExist != null)
                {
                    // if record exist then update the existing data
                    if (x.AssignmentStatus == true && !string.IsNullOrWhiteSpace(x.AssignmentDate))
                    {
                        RecodrExist.ExpiryDate = DateTime.ParseExact(x.AssignmentDate + " 23:59:59", ConfigurationManager.AppSettings["dateformatForCalanderServerSide"].ToString(), CultureInfo.InvariantCulture);
                        RecodrExist.AssignedStatus = true;
                        Common.CourseGroupUserAssignmentSendMail("CGASS", x.GroupId, x.CourseId);
                    }
                    else
                    {
                        RecodrExist.ExpiryDate = null;
                        RecodrExist.AssignedStatus = false;

                    }
                    RecodrExist.DateLastModified = DateTime.Now;
                    RecodrExist.LastModifiedByID = Convert.ToInt64(Session["UserID"]);
                    db.SaveChanges();
                }
                else if (x.AssignmentStatus == true && !string.IsNullOrWhiteSpace(x.AssignmentDate))
                {
                    // create the new record indatabase as record not exist.
                    GroupCourse NewRecord = new GroupCourse();
                    NewRecord.CourseId = x.CourseId;
                    NewRecord.GroupID = x.GroupId;
                    NewRecord.ExpiryDate = DateTime.ParseExact(x.AssignmentDate + " 23:59:59", ConfigurationManager.AppSettings["dateformatForCalanderServerSide"].ToString(), CultureInfo.InvariantCulture);
                    NewRecord.AssignedStatus = true;
                    NewRecord.CreationDate = DateTime.Now;
                    NewRecord.CreatedById = Convert.ToInt64(Session["UserID"]);
                    db.GroupCourses.Add(NewRecord);
                    db.SaveChanges();

                    Common.CourseGroupUserAssignmentSendMail("CGASS", x.GroupId, x.CourseId);
                }
            }
        }

        #endregion

        #region // Assign courses to user
        [HttpGet]
        public ActionResult AjaxHandlerAssignedCoursesToUser(jQueryDataTableParamModel param)
        {
            var sortColumnIndex = Convert.ToInt32(Request["iSortCol_0"]);
            Func<Assigned_Course_T_Users, string> orderingFunction = (c => sortColumnIndex == 0 ? c.FirstName.TrimEnd().TrimStart().ToLower() :
                                                            sortColumnIndex == 1 ? c.LastName.TrimEnd().TrimStart().ToLower() :
                                                            sortColumnIndex == 2 ? c.EmailAddress.TrimEnd().TrimStart().ToLower() :
                                                            sortColumnIndex == 3 ? c.OrganisationName.TrimEnd().TrimStart().ToLower() :
                                                            sortColumnIndex == 5 ? c.AssignedStatus.ToString() :
                                                            c.FirstName.ToLower());
            var sortDirection = Request["sSortDir_0"];// get the sort direction.
            IEnumerable<Assigned_Course_T_Users> filterUserAssignedCourse = null;
            long? CourseId = Int64.Parse(param.iD.ToString());
            int languageId = 0;
            languageId = int.Parse(Session["LanguageId"].ToString());
            try
            {
                var GetResult = db.GetAssignedCoursesToUser((Common.IsAdmin() ? 0 : Convert.ToInt64(Session["UserID"])), languageId, CourseId); // get the date from stored procedure.
                var tempx = from x in GetResult
                            select new Assigned_Course_T_Users
                            {
                                UserId = x.UserId,
                                FirstName = x.FirstName,
                                LastName = x.LastName,
                                OrganisationName = x.OrganisationName,
                                EmailAddress = x.EmailAddress,
                                ExpiryDate = x.ExpiryDate,
                                AssignedStatus = x.AssignedStatus == true ? true : false
                            };
                filterUserAssignedCourse = tempx.ToList<Assigned_Course_T_Users>();

                /// search action
                if (!string.IsNullOrEmpty(param.sSearch))
                {
                    filterUserAssignedCourse = from x in filterUserAssignedCourse
                                               where x.FirstName.ToLower().Contains(param.sSearch.ToLower()) || x.OrganisationName.ToLower().Contains(param.sSearch.ToLower()) || x.EmailAddress.ToLower().Contains(param.sSearch.ToLower())
                                               select x;

                }
                else
                {
                    filterUserAssignedCourse = from x in filterUserAssignedCourse
                                               select x;
                }

                // ordering action
                if (sortDirection == "asc")
                {
                    filterUserAssignedCourse = filterUserAssignedCourse.OrderBy(orderingFunction);
                }
                else if (sortDirection == "desc")
                {
                    filterUserAssignedCourse = filterUserAssignedCourse.OrderByDescending(orderingFunction);
                }

                filterUserAssignedCourse = filterUserAssignedCourse.ToList();

                // records to display            
                var displayedUserAssignedCourse = filterUserAssignedCourse.Skip(param.iDisplayStart).Take(param.iDisplayLength);
                if (param.iDisplayLength == -1)
                    displayedUserAssignedCourse = filterUserAssignedCourse;

                // create return object.
                var result = from obj in displayedUserAssignedCourse.ToList()
                             select new[] {
                              obj.FirstName,
                              obj.LastName,
                              obj.EmailAddress,
                              obj.OrganisationName,
                              obj.ExpiryDate==null?"":((DateTime)obj.ExpiryDate).ToString(ConfigurationManager.AppSettings["dateformatForCalanderServerSide"].ToString(),CultureInfo.InvariantCulture),
                              obj.AssignedStatus.ToString(),
                              Convert.ToString(obj.UserId),

                          };
                // return the object in json format.
                return Json(new
                {
                    sEcho = param.sEcho,
                    iTotalRecords = filterUserAssignedCourse.Count(),
                    iTotalDisplayRecords = filterUserAssignedCourse.Count(),
                    aaData = result
                },
                               JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {

                },
                                  JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public ActionResult AssignCourseToUser(int id)
        {
            var Courseexist = db.Courses.Find(id); // find the course 
            Course_Assigned_Course_T_Users us = new Course_Assigned_Course_T_Users();

            if (Courseexist != null)
            {
                if (Courseexist.IsFinalized == true && Courseexist.IsDeleted == false) // check the course is not deleted.
                {
                    us.CourseId = Courseexist.CourseId;
                    us.CourseName = Courseexist.CourseName;
                    us.DateFormatForClientSide = ConfigurationManager.AppSettings["dateformatForCalanderClientSide"].ToString(); // sets the client side date format.
                }
                else // if course not exist then redirect it to course index page.
                {
                    return RedirectToAction("Index", "Course");
                }

            }
            return View(us);
        }

        //http post method to assign courses to users       
        [HttpPost]
        public void AssignCourseToUser(List<Submit_Assigned_Course_T_Users> model)
        {
            foreach (var x in model)
            {
                // check user and course relation ship exist in database or not. ie course is assigned to user or not.
                var RecodrExist = db.UserCourses.Where(y => y.UserId == x.UserId && y.CourseId == x.CourseId).FirstOrDefault();
                if (RecodrExist != null) // if exist then update the existing record
                {
                    if (x.AssignmentStatus == true && !string.IsNullOrWhiteSpace(x.AssignmentDate))
                    {
                        RecodrExist.ExpiryDate = DateTime.ParseExact(x.AssignmentDate + " 23:59:59", ConfigurationManager.AppSettings["dateformatForCalanderServerSide"].ToString(), CultureInfo.InvariantCulture);
                        RecodrExist.AssignedStatus = true;
                        Common.CourseGroupUserAssignmentSendMail("CGASS", 0, x.CourseId, x.UserId);
                    }
                    else
                    {
                        RecodrExist.ExpiryDate = null;
                        RecodrExist.AssignedStatus = false;
                    }
                    RecodrExist.DateLastModified = DateTime.Now;
                    RecodrExist.LastModifiedByID = Convert.ToInt64(Session["UserID"]);
                    db.SaveChanges();
                } // create the new record in database.
                else if (x.AssignmentStatus == true && !string.IsNullOrWhiteSpace(x.AssignmentDate))
                {
                    UserCourse NewRecord = new UserCourse();
                    NewRecord.CourseId = x.CourseId;
                    NewRecord.UserId = x.UserId;
                    NewRecord.ExpiryDate = DateTime.ParseExact(x.AssignmentDate + " 23:59:59", ConfigurationManager.AppSettings["dateformatForCalanderServerSide"].ToString(), CultureInfo.InvariantCulture);
                    NewRecord.AssignedStatus = true;
                    NewRecord.CreationDate = DateTime.Now;
                    NewRecord.CreatedById = Convert.ToInt64(Session["UserID"]);
                    db.UserCourses.Add(NewRecord);
                    db.SaveChanges();

                    Common.CourseGroupUserAssignmentSendMail("CGASS", 0, x.CourseId, x.UserId);
                }
            }
        }


        #endregion
    }
}