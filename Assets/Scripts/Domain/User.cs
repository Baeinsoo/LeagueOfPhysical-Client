namespace LOP
{
    public class User
    {
        public string id;
        //  표시용 이름. 유일하지 않다 — 신원은 tag가 가른다.
        public string displayName;
        //  Crockford Base32 6자리. 가입 때 서버가 부여하고 바뀌지 않는다.
        public string tag;
        public string email;
    }
}
