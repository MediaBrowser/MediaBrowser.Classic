using MediaBrowser.Library.Entities;

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
                    if (value)
                    {
                        Kernel.ApiClient.AddUserToSession(BaseItem.ApiId);
                    }
                    else
                    {
                        Kernel.ApiClient.RemoveUserFromSession(BaseItem.ApiId);
                    }
                    FirePropertyChanged("IsAlsoHere");
                }
            }
        }
    }
}
