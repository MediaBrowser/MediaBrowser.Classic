using System.Linq;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Threading;

namespace MediaBrowser.Library
{
    public class UserItem : Item
    {
        public static bool IsOne(BaseItem item)
        {
            return item is User;
        }

        public User User {get { return BaseItem as User; }}

        public bool IsAlsoHere
        {
            get { return User.IsAlsoHere; }

            set
            {
                if (User.IsAlsoHere != value)
                {
                    User.IsAlsoHere = value;
                    Async.Queue(Async.ThreadPoolName.AddUser, () =>
                    {
                        if (value)
                        {
                            Application.CurrentInstance.MultipleUsersHere = true;
                            Kernel.ApiClient.AddUserToSession(BaseItem.ApiId);
                        }
                        else
                        {
                            Application.CurrentInstance.MultipleUsersHere = Application.CurrentInstance.OtherAvailableUsers.Cast<UserItem>().Any(u => u.IsAlsoHere);
                            Kernel.ApiClient.RemoveUserFromSession(BaseItem.ApiId);
                        }
                                                    
                    });
                    UIFirePropertyChange("IsAlsoHere");
                }
            }
        }
    }
}
