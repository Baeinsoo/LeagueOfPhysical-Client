namespace LOP
{
    public class ChangeDisplayNameResponse : HttpResponse
    {
        //  실패(형식 위반)면 서버가 user를 안 싣는다 — 읽는 쪽은 code를 먼저 본다.
        public UserDto user;
    }
}
