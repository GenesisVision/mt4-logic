//+------------------------------------------------------------------+
//|                                       MetaTrader WebRegistration |
//|                 Copyright © 2001-2006, MetaQuotes Software Corp. |
//|                                        http://www.metaquotes.net |
//+------------------------------------------------------------------+
#include "stdafx.h"
#include "stringfile.h"
//+------------------------------------------------------------------+
//| Constructor                                                      |
//+------------------------------------------------------------------+
CStringFile::CStringFile(const int nBufSize) :
             m_file(INVALID_HANDLE_VALUE),m_file_size(0),
             m_buffer(new BYTE[nBufSize]),m_buffer_size(nBufSize),
             m_buffer_index(0),m_buffer_readed(0),m_buffer_line(0)
  {
  }
//+------------------------------------------------------------------+
//| Destructor                                                       |
//+------------------------------------------------------------------+
CStringFile::~CStringFile()
  {
//---- close
   Close();
//---- dispose buffer
   if(m_buffer!=NULL) { delete[] m_buffer; m_buffer=NULL; }
  }
//+------------------------------------------------------------------+
//| File opening                                                     |
//| dwAccess       -GENERIC_READ или GENERIC_WRITE                   |
//| dwCreationFlags-CREATE_ALWAYS, OPEN_EXISTING, OPEN_ALWAYS        |
//+------------------------------------------------------------------+
bool CStringFile::Open(LPCTSTR lpFileName,const DWORD dwAccess,const DWORD dwCreationFlags)
  {
//---- close
   Close();
//---- check
   if(lpFileName!=NULL)
     {
      //----
      m_file=CreateFile(lpFileName,dwAccess,FILE_SHARE_READ | FILE_SHARE_WRITE,
                        NULL,dwCreationFlags,FILE_ATTRIBUTE_NORMAL,NULL);
      //----
      if(m_file!=INVALID_HANDLE_VALUE) m_file_size=GetFileSize(m_file,NULL);
     }
//---- return result
   return(m_file!=INVALID_HANDLE_VALUE);
  }
//+------------------------------------------------------------------+
//| Read buffer of the specified length                              |
//+------------------------------------------------------------------+
int CStringFile::Read(void *buffer,const DWORD length)
  {
   DWORD readed=0;
//---- check
   if(m_file==INVALID_HANDLE_VALUE || buffer==NULL || length<1) return(0);
//---- read
   if(ReadFile(m_file,buffer,length,&readed,NULL)==0) readed=0;
//---- return readed bytes
   return(readed);
  }
//+------------------------------------------------------------------+
//| Write into file                                                  |
//+------------------------------------------------------------------+
int CStringFile::Write(const void *buffer,const DWORD length)
  {
   DWORD written=0;
//---- check
   if(m_file==INVALID_HANDLE_VALUE || buffer==NULL || length<1) return(0);
//---- write
   if(WriteFile(m_file,buffer,length,&written,NULL)==0) written=0;
//---- return written bytes
   return(written);
  }
//+------------------------------------------------------------------+
//| Reset                                                            |
//+------------------------------------------------------------------+
void CStringFile::Reset()
  {
//----
   m_buffer_index=0;
   m_buffer_readed=0;
   m_buffer_line=0;
//---- set file pointer on the begining
   if(m_file!=INVALID_HANDLE_VALUE) SetFilePointer(m_file,0,NULL,FILE_BEGIN);
  }
//+------------------------------------------------------------------+
//| Read next text line from the file                                |
//+------------------------------------------------------------------+
int CStringFile::GetNextLine(char *line,const int maxsize)
  {
   char  *currsym=line,*lastsym=line+maxsize;
   BYTE  *curpos=m_buffer+m_buffer_index;
//---- check
   if(line==NULL || m_file==INVALID_HANDLE_VALUE || m_buffer==NULL) return(0);
//---- infinite loop
   for(;;)
     {
      //---- check buffer
      if(m_buffer_line==0 || m_buffer_index==m_buffer_readed)
        {
         //----
         m_buffer_index=0;
         m_buffer_readed=0;
         //---- read in the buffer
         if(::ReadFile(m_file,m_buffer,m_buffer_size,(DWORD*)&m_buffer_readed,NULL)==0)
           {
            Close();
            return(0);
           }
         //---- read 0 bytes?-this is the end of file!
         if(m_buffer_readed<1) { *currsym=0; return(currsym!=line ? m_buffer_line:0); }
         curpos=m_buffer;
        }
      //---- parse buffer
      while(m_buffer_index<m_buffer_readed)
        {
         //---- is this the end?
         if(currsym>=lastsym) { *currsym=0; return(m_buffer_line); }
         //---- check symbol...
         if(*curpos=='\n')
           {
            //---- did the '\r' before?
            if(currsym>line && currsym[-1]=='\r') currsym--; // clean it
            *currsym=0;
            //---- return number of the string
            m_buffer_line++;
            m_buffer_index++;
            return(m_buffer_line);
           }
         //---- just copy
         *currsym++=*curpos++; m_buffer_index++;
        }
     }
//---- this is impossible...
   return(0);
  }
//+------------------------------------------------------------------+
