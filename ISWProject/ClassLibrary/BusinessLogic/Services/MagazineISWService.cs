﻿using Magazine.Entities;
using Magazine.Persistence;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace Magazine.Services
{
    public class MagazineISWService : IMagazineISWService
    {
        // Dal persistence service 
        private readonly IDAL dal;

        // Resources Manager for error messaging
        private ResourceManager resourceManager;

        // logged in User for verification purposes such as (not complete list):
        // - operations restricted to area editors
        // - operations restricted to chief editor
        // - submitted paper responsible author is logged in user
        private User loggedUser;

        private Entities.Magazine magazine;
        public  MagazineISWService(IDAL dal){
            this.dal = dal;

            // Resource manager for internationalization of error messages is created
            resourceManager = new ResourceManager("MagazineLib.Resources.ExceptionMessages", Assembly.GetExecutingAssembly());
            
            // Only one magazine object exists in our system
            magazine = dal.GetAll<Entities.Magazine>().FirstOrDefault();
            if(magazine == null)
            {
                DBInitialization();
            }
        }

        public void Commit()
        {
            dal.Commit();
        }

        public bool IsValidEmail(string email) 
        {
            if (email == null || email.Length <= 4) return false;
            int indexAt = email.IndexOf('@');
            int indexDot = email.LastIndexOf('.');
            if (indexAt == -1 || indexDot == -1) return false;
            if (indexAt > indexDot) return false;
            if (indexDot - indexAt <= 2) return false;
            if (email.Length - indexDot < 2) return false;
            return true;
        }

        public bool IsValidPassword(string password)
        {
            if (password == null || password.Length < 8) return false;
            int conds = 0;
            foreach(char c in password)
            {
                if (c >= 'a' && c <= 'z')
                {
                    conds++;
                    break;
                }
            }
            foreach(char c in password)
            {
                if (c >= 'A' && c <= 'Z')
                {
                    conds++;
                    break;
                }
            }
            foreach(char c in password)
            {
                if (c >= '0' && c <= '9')
                {
                    conds++;
                    break;
                }
            }
            char[] specialCharacters = {'?', '-', '+', '=', '_', '@', '#', '!', '&','$'};
            if (password.IndexOfAny(specialCharacters) != -1) conds++;
            if (conds < 4) return false;
            return true;
        }

        public bool IsValidUser(string user)
        {
            if(user == null || user.Length == 0 || user.Length > 30) return false;
            return true;
        }

        private void ValidateLoggedUser(bool validateLogged)
        {
            if (validateLogged) {
                // if user is not logged in and it should be logged in then generate exception
                if (loggedUser == null) throw new ServiceException(resourceManager.GetString("LoggedOutUser"));
            }
            else // if user is logged in and it should not be logged in then generate exception 
                if (loggedUser != null) throw new ServiceException(resourceManager.GetString("LoggedUser"));

        }

        public void DBInitialization()
        {
            // Chief editor registered
            RegisterUser("66666666A", "Javier", "Jaen", false, "HCI; Software Engineering", "fjaen@upv.es", "fjaen", "1234");
            
            // Area editors registered
            RegisterUser("77777777B", "Jorge", "Montaner", false, "Software Engineering", "jormonm5@upv.es", "jmontaner", "1234");
            RegisterUser("88888888C", "Fernando", "Alonso", false, "HCI", "falonso@upv.es", "falonso", "1234");
            
            // Author registered
            RegisterUser("99999999D", "Carlos", "Sainz", false, "HCI", "csainz@upv.es", "csainz", "1234");
            
            // Magazine created and stored in "magazine" reference
            int magazineId = AddMagazine("University Magazine","66666666A");
            magazine = dal.GetById<Entities.Magazine>(magazineId);
            
            // Two Areas added, Login required because only chief editor is allowed to do this 
            Login("fjaen", "1234");
            AddArea("HCI", "77777777B");
            AddArea("Software Engineering", "88888888C");
            Logout();
        }

        #region User
        public void RegisterUser(string id, string name, string surname, bool alerted, string areasOfInterest, string email, string login, string password)
        {
            if (dal.GetById<User>(id) != null) throw new ServiceException(resourceManager.GetString("LoggedUser"));
            if ((name == null) || (name.Length < 2)) throw new ServiceException(resourceManager.GetString("InvalidUserName"));
            if ((surname == null) || (surname.Length < 2)) throw new ServiceException(resourceManager.GetString("InvalidUserSurname"));
            if (!IsValidEmail(email)) throw new ServiceException(resourceManager.GetString("InvalidEmail"));
            if (!IsValidUser(login)) throw new ServiceException(resourceManager.GetString("InvalidUser"));
            if (!IsValidPassword(password)) throw new ServiceException(resourceManager.GetString("InvalidPassword"));
            Magazine.Entities.User regUser = new Magazine.Entities.User(id, name, surname, alerted, areasOfInterest, email, login, password);
            dal.Insert<User>(regUser);
            Commit();
            
        }

        public string Login(string login, string password)
        {   
            if(!IsValidUser(login)) { throw new ServiceException(resourceManager.GetString("InvalidUser"));}
            if(!IsValidPassword(password)) { throw new ServiceException(resourceManager.GetString("InvalidPassword"));}
            User myUser = dal.GetWhere<User>((u) => u.Login.Equals(login)).ToList().FirstOrDefault(null);
            if (myUser == null) {throw new ServiceException(resourceManager.GetString("UserNotExists"));}
            if (!myUser.Password.Equals(password)) { throw new ServiceException(resourceManager.GetString("IncorrectPassword"));}
            else { loggedUser = myUser; return myUser.Id; }
        }

        public void Logout() {
            loggedUser = null;
        }

        #endregion

        #region Paper
        public int SubmitPaper(int areaId, string title, DateTime uploadDate)
        {
            ValidateLoggedUser(true);
            Area area = magazine.GetAreaById(areaId);
            if (area == null) throw new ServiceException(resourceManager.GetString("InvalidAreaName"));
            Paper paper = new Paper(title, uploadDate, area, loggedUser);
            area.Papers.Add(paper);
            area.EvaluationPending.Add(paper);
            paper.EvaluationPendingArea = area;
            dal.Insert(area);
            dal.Insert(paper);
            Commit();
            return paper.Id;
        }

        public void RegisterPerson(string id, string name, string surname)
        {
            if (dal.GetById<Paper>(id) != null) throw new ServiceException(resourceManager.GetString("LoggedPerson"));
            if ((name == null) || (name.Length < 2)) throw new ServiceException(resourceManager.GetString("InvalidUserName"));
            if ((surname == null) || (surname.Length < 2)) throw new ServiceException(resourceManager.GetString("InvalidUserSurname"));
            Magazine.Entities.Person regPerson = new Magazine.Entities.Person(id, name, surname);
            dal.Insert<Person>(regPerson);
            Commit();
        }
        public Person GetPersonById (string id) 
        {
            Person person = dal.GetById<Person>(id);
            if (person != null) return person;
            else throw new ServiceException(resourceManager.GetString("PersonNotExists"));
        }
        public void AddCoauthor (int paperId, string id)
        {
            Paper paper = magazine.GetPaperById(paperId);
            Person person = GetPersonById(id);
            paper.AddCoauthor(person);
            Commit();
        }

        public void EvaluatePaper(bool accepted, string comments, DateTime date, int paperId)
        {
            ValidateLoggedUser(true);
            Evaluation evaluation = new Evaluation(accepted, comments, date);
            dal.Insert(evaluation);
            Paper paper = magazine.GetEvPendingPaperById(paperId);
            paper.Evaluation = evaluation;
            paper.EvaluationPendingArea = null;
            paper.PublicationPendingArea = paper.BelongingArea;
            Commit();
        }
        #endregion


        #region Issue
        public int AddIssue(int number)
        {
            try 
            { 
                magazine.Issues.Add(new Issue(number, magazine));
                return number;
            }
            catch (Exception) { return -1; }
        }

        public void GetLastIssue() //dentro de magazine
        {
            int res = Int32.MinValue;
            Issue issue = null;
            foreach (Issue i in magazine.Issues)
                if (i.Number > issue.Number) issue = i;
            
            int added = issue.PublicationDate == null ? AddIssue(issue.Number) : AddIssue(issue.Number + 1);
        }

        public List<Area> GetAllAreas()
        {
            return magazine.Areas.ToList<Area>();
        }

        public List<Paper> GetAllPendingPapers()
        {
            List<Paper> paperList = new List<Paper>();
            foreach (Area area in magazine.Areas)
                paperList.Concat(area.PublicationPending.ToList<Paper>());

            return paperList;
        }

        //public List

        public void ModifyIssue(int Id, DateTime newPublicationDate)
        {
            Issue issue = null;

            foreach (Issue i in magazine.Issues)
                if (i.Id == Id)
                {
                    issue = i;
                    //magazine.Issues.Remove(i);
                }

            issue.PublicationDate = newPublicationDate;
            //magazine.Issues.Add(issue);
            Commit();
        }

        #endregion

        #region Area


        /// <summary>   Validate area and if correct, add a new area.</summary>
        /// <param>     <c>areaName</c> is the area name. 
        /// </param>     
        /// <param>     <c>editorId</c> is the Id of the area editor. 
        /// </param>
        /// <returns> 
        ///             Area id
        ///             Any required ServiceExceptions
        /// </returns>
        public int AddArea(string areaName, string editorId)
        {
            // validate logged user
            ValidateLoggedUser(true);

            // validate user is chief editor
            if (loggedUser.Magazine==null) throw new ServiceException(resourceManager.GetString("InvalidAddAreaUser"));

            // validate magazine exists
            if (magazine == null) throw new ServiceException(resourceManager.GetString("MagazineNotExists"));

            // validate area name not empty
            if ((areaName == null)||(areaName.Length == 0)) throw new ServiceException(resourceManager.GetString("InvalidAreaName"));
            
            // validate not another area with same name
            if (magazine.GetAreaByName(areaName) != null) throw new ServiceException(resourceManager.GetString("InvalidAreaName"));
            
            // validate editor id not empty
            if ((editorId == null) || (editorId.Length == 0)) throw new ServiceException(resourceManager.GetString("NullUserId"));

            //validate editor exists
            var editor = dal.GetById<User>(editorId);
            if (editor==null) throw new ServiceException(resourceManager.GetString("UserNotExists"));            

            // inserts area
            Area area = new Area(areaName, editor, magazine);
            magazine.AddArea(area);
            Commit();
            return area.Id;
        }

        #endregion

        #region Magazine

        /// <summary>   Validate data and if correct, add a new magazine with its chief editor.</summary>
        /// <param>     <c>name</c> is the name of the magazine 
        /// </param>
        /// <param>     <c>chiefEditorId</c> is the is the user Id of that becomes chief Editor 
        /// </param>
        /// <returns>   
        ///             Magazine Id
        ///             Any required Service Exceptions 
        /// </returns>
        public int AddMagazine(string name, string chiefEditorId)
        {
            // validate magazine name not empty
            if ((name == null)||(name.Equals(""))) throw new ServiceException(resourceManager.GetString("InvalidMagazineName"));            
            
            // validate chief editor id not empty
            if ((chiefEditorId==null)||(chiefEditorId.Length==0)) throw new ServiceException(resourceManager.GetString("NullUserId"));
            
            // validate chief editor exists
            var chief =dal.GetById<User>(chiefEditorId);
            if (chief == null) throw new ServiceException(resourceManager.GetString("UserNotExists"));

            // insert magazine
            Entities.Magazine m = new Entities.Magazine(name, chief);
            dal.Insert(m);
            Commit();
            return m.Id;
        }

        List<Paper> ListAllPapers()
        {
            List<Paper> list = new List<Paper>();
            foreach(Area a in magazine.Areas) 
                list.Concat(a.Papers);

            return list;
        }

        #endregion
    }
}
